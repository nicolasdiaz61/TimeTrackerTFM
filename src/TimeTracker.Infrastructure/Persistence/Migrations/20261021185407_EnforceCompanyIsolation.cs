using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeTracker.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Migración B «contraer» del cambio multi-compania (DD-4): hace obligatorio <c>company_id</c>, crea el índice único
    /// <c>(company_id, id)</c> de <c>users</c> y las claves foráneas (las compuestas cierran el hueco del ADR 0011), pasa la clave
    /// primaria de <c>assistant_settings</c> a <c>company_id</c> (sin <c>id</c> ni fila única) y retira el <c>DEFAULT</c> de la auditoría.
    /// Se escribe a mano en SQL; el <c>Designer</c> y el snapshot son los generados por EF.
    /// Antes de tocar nada rellena lo que escribió el código anterior con <c>company_id</c> nulo y comprueba los fichajes huérfanos:
    /// si los hay, aborta con un mensaje claro (solo el número de filas) y sin cambios parciales, porque el DDL de PostgreSQL es
    /// transaccional. B nunca borra ni inventa usuarios: qué hacer con los huérfanos lo decide una persona antes.
    /// Los triggers de solo inserción de la auditoría no se tocan: ninguna sentencia de esta migración actualiza ni borra sus filas.
    /// El <c>Down</c> aborta si existe más de una compañía (la separación entre compañías se perdería); no depende del número de
    /// filas de configuración. Con una sola compañía deja el esquema como lo dejó A, salvo que <c>assistant_settings.id</c>
    /// vuelve con <c>DEFAULT 1</c> (spec <c>company-isolation</c>).
    /// </summary>
    public partial class EnforceCompanyIsolation : Migration
    {
        // Mismo valor que Company.DefaultId (una prueba compara ambos); se escribe literal para que la migración no cambie si cambia el código.
        private const string DefaultCompanyId = "00000000-0000-7000-8000-000000000001";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Relleno residual de lo que escribió el código anterior durante la convivencia (la auditoría tiene DEFAULT).
            migrationBuilder.Sql($"UPDATE users SET company_id = '{DefaultCompanyId}' WHERE company_id IS NULL;");
            migrationBuilder.Sql($"UPDATE time_entries SET company_id = '{DefaultCompanyId}' WHERE company_id IS NULL;");
            migrationBuilder.Sql($"UPDATE assistant_settings SET company_id = '{DefaultCompanyId}' WHERE company_id IS NULL;");

            // 2. Fichajes huérfanos: se aborta antes de cambiar nada. El mensaje lleva solo el número, nunca identificadores.
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    orphans bigint;
                BEGIN
                    SELECT count(*) INTO orphans
                    FROM time_entries entry
                    WHERE NOT EXISTS (SELECT 1 FROM users u WHERE u.id = entry.employee_id);
                    IF orphans > 0 THEN
                        RAISE EXCEPTION 'time_entries huérfanos: %', orphans
                            USING HINT = 'Exporte y borre esos fichajes, o reasígnelos a un usuario real, y vuelva a aplicar la migración.';
                    END IF;
                END
                $$;
                """);

            // 3. Obligatoriedad. La auditoría ya era NOT NULL: solo se retira el DEFAULT (ya no puede haber escritores antiguos).
            migrationBuilder.Sql("ALTER TABLE users ALTER COLUMN company_id SET NOT NULL;");
            migrationBuilder.Sql("ALTER TABLE time_entries ALTER COLUMN company_id SET NOT NULL;");
            migrationBuilder.Sql("ALTER TABLE assistant_settings ALTER COLUMN company_id SET NOT NULL;");
            migrationBuilder.Sql("ALTER TABLE assistant_settings_audit ALTER COLUMN company_id DROP DEFAULT;");

            // 4. users: destino de las claves compuestas, FK a companies y FK compuesta del responsable (NO ACTION, MATCH SIMPLE).
            migrationBuilder.Sql("ALTER TABLE users ADD CONSTRAINT ux_users_company_id_id UNIQUE (company_id, id);");
            migrationBuilder.Sql("ALTER TABLE users ADD CONSTRAINT fk_users_companies_company_id FOREIGN KEY (company_id) REFERENCES companies (id) ON DELETE NO ACTION;");
            migrationBuilder.Sql("ALTER TABLE users DROP CONSTRAINT fk_users_users_manager_id;");
            migrationBuilder.Sql("ALTER TABLE users ADD CONSTRAINT fk_users_users_company_id_manager_id FOREIGN KEY (company_id, manager_id) REFERENCES users (company_id, id) ON DELETE NO ACTION;");
            migrationBuilder.Sql("DROP INDEX ix_users_manager_id;");
            migrationBuilder.Sql("CREATE INDEX ix_users_company_id_manager_id ON users (company_id, manager_id);");

            // 5. time_entries: FK a companies y FK compuesta del empleado (cierra el hueco del ADR 0011).
            migrationBuilder.Sql("ALTER TABLE time_entries ADD CONSTRAINT fk_time_entries_companies_company_id FOREIGN KEY (company_id) REFERENCES companies (id) ON DELETE NO ACTION;");
            migrationBuilder.Sql("ALTER TABLE time_entries ADD CONSTRAINT fk_time_entries_users_company_id_employee_id FOREIGN KEY (company_id, employee_id) REFERENCES users (company_id, id) ON DELETE NO ACTION;");
            migrationBuilder.Sql("CREATE INDEX ix_time_entries_company_id_employee_id ON time_entries (company_id, employee_id);");

            // 6. assistant_settings: una fila por compañía (la clave primaria es company_id; sin id ni fila única).
            migrationBuilder.Sql("ALTER TABLE assistant_settings DROP CONSTRAINT ck_assistant_settings_singleton;");
            migrationBuilder.Sql("ALTER TABLE assistant_settings DROP CONSTRAINT \"PK_assistant_settings\";");
            migrationBuilder.Sql("ALTER TABLE assistant_settings DROP COLUMN id;");
            migrationBuilder.Sql("ALTER TABLE assistant_settings ADD CONSTRAINT \"PK_assistant_settings\" PRIMARY KEY (company_id);");
            migrationBuilder.Sql("ALTER TABLE assistant_settings ADD CONSTRAINT fk_assistant_settings_companies_company_id FOREIGN KEY (company_id) REFERENCES companies (id) ON DELETE NO ACTION;");

            // 7. Auditoría: FK a companies e índice por compañía y fecha (sin tocar los triggers de solo inserción).
            migrationBuilder.Sql("ALTER TABLE assistant_settings_audit ADD CONSTRAINT fk_assistant_settings_audit_companies_company_id FOREIGN KEY (company_id) REFERENCES companies (id) ON DELETE NO ACTION;");
            migrationBuilder.Sql("DROP INDEX ix_assistant_settings_audit_occurred_at;");
            migrationBuilder.Sql("CREATE INDEX ix_assistant_settings_audit_company_id_occurred_at ON assistant_settings_audit (company_id, occurred_at DESC);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Con más de una compañía el Down perdería la separación: se aborta sin modificar nada. La condición es el número de
            // compañías (o de company_id distintos en users), nunca el de filas de assistant_settings.
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    companies_count bigint;
                    user_companies bigint;
                BEGIN
                    SELECT count(*) INTO companies_count FROM companies;
                    SELECT count(DISTINCT company_id) INTO user_companies FROM users;
                    IF companies_count > 1 OR user_companies > 1 THEN
                        RAISE EXCEPTION 'No se puede revertir EnforceCompanyIsolation: existen varias compañías (compañías: %).', GREATEST(companies_count, user_companies)
                            USING HINT = 'Con varias compañías no hay Down: restaure la copia de seguridad o corrija hacia delante.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.Sql("DROP INDEX ix_assistant_settings_audit_company_id_occurred_at;");
            migrationBuilder.Sql("CREATE INDEX ix_assistant_settings_audit_occurred_at ON assistant_settings_audit (occurred_at DESC);");
            migrationBuilder.Sql("ALTER TABLE assistant_settings_audit DROP CONSTRAINT fk_assistant_settings_audit_companies_company_id;");

            migrationBuilder.Sql("ALTER TABLE assistant_settings DROP CONSTRAINT fk_assistant_settings_companies_company_id;");
            migrationBuilder.Sql("ALTER TABLE assistant_settings DROP CONSTRAINT \"PK_assistant_settings\";");
            migrationBuilder.Sql("ALTER TABLE assistant_settings ADD COLUMN id smallint NOT NULL DEFAULT 1;");
            migrationBuilder.Sql("ALTER TABLE assistant_settings ADD CONSTRAINT \"PK_assistant_settings\" PRIMARY KEY (id);");
            migrationBuilder.Sql("ALTER TABLE assistant_settings ADD CONSTRAINT ck_assistant_settings_singleton CHECK (id = 1);");

            migrationBuilder.Sql("DROP INDEX ix_time_entries_company_id_employee_id;");
            migrationBuilder.Sql("ALTER TABLE time_entries DROP CONSTRAINT fk_time_entries_users_company_id_employee_id;");
            migrationBuilder.Sql("ALTER TABLE time_entries DROP CONSTRAINT fk_time_entries_companies_company_id;");

            migrationBuilder.Sql("ALTER TABLE users DROP CONSTRAINT fk_users_users_company_id_manager_id;");
            migrationBuilder.Sql("DROP INDEX ix_users_company_id_manager_id;");
            migrationBuilder.Sql("ALTER TABLE users ADD CONSTRAINT fk_users_users_manager_id FOREIGN KEY (manager_id) REFERENCES users (id);");
            migrationBuilder.Sql("CREATE INDEX ix_users_manager_id ON users (manager_id);");
            migrationBuilder.Sql("ALTER TABLE users DROP CONSTRAINT fk_users_companies_company_id;");
            migrationBuilder.Sql("ALTER TABLE users DROP CONSTRAINT ux_users_company_id_id;");

            // Como A: anulables, salvo la auditoría, que vuelve a tener el DEFAULT constante.
            migrationBuilder.Sql("ALTER TABLE users ALTER COLUMN company_id DROP NOT NULL;");
            migrationBuilder.Sql("ALTER TABLE time_entries ALTER COLUMN company_id DROP NOT NULL;");
            migrationBuilder.Sql("ALTER TABLE assistant_settings ALTER COLUMN company_id DROP NOT NULL;");
            migrationBuilder.Sql($"ALTER TABLE assistant_settings_audit ALTER COLUMN company_id SET DEFAULT '{DefaultCompanyId}';");
        }
    }
}
