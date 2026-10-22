using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeTracker.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Migración A «expandir» del cambio multi-compania (DD-4, DD-5): crea <c>companies</c> con la compañía por defecto y añade
    /// <c>company_id</c> relleno con ella. Es aditiva y segura para el código anterior: no crea claves foráneas, claves únicas
    /// ni índices nuevos sobre las tablas existentes (los crea la migración B) y las filas que escriba el código anterior
    /// mientras conviva quedan con <c>company_id</c> nulo, que B rellena.
    /// El <c>Designer</c> y el snapshot reflejan el modelo final (<c>CompanyId</c> no nulo): EF solo compara el snapshot con el
    /// modelo, no con la base de datos, y así <c>has-pending-model-changes</c> queda vacío tras B.
    /// La auditoría se rellena sin ningún <c>UPDATE</c> (el trigger de solo inserción lo rechazaría): <c>ADD COLUMN ... NOT NULL DEFAULT</c>
    /// con una constante se guarda como metadato en PostgreSQL 11 o superior, sin reescribir la tabla ni ejecutar el trigger.
    /// <c>xmin</c> de <c>users</c> es la columna de sistema de PostgreSQL (token de concurrencia): no se crea nada.
    /// </summary>
    public partial class AddCompanies : Migration
    {
        // Mismo valor que Company.DefaultId (una prueba compara ambos); se escribe literal para que la migración no cambie si cambia el código.
        private const string DefaultCompanyId = "00000000-0000-7000-8000-000000000001";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE companies (
                    id uuid NOT NULL,
                    name character varying(100) NOT NULL,
                    name_normalized character varying(100) NOT NULL,
                    status character varying(16) NOT NULL DEFAULT 'Active',
                    uses_platform_assistant boolean NOT NULL DEFAULT false,
                    created_at timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_companies" PRIMARY KEY (id),
                    CONSTRAINT ck_companies_name_length CHECK (char_length(btrim(name)) BETWEEN 1 AND 100),
                    CONSTRAINT ck_companies_status CHECK (status IN ('Active', 'Suspended'))
                );
                """);
            migrationBuilder.Sql("CREATE UNIQUE INDEX ux_companies_name_normalized ON companies (name_normalized);");

            // Nombre ASCII para que lower() de SQL y Company.NormalizeName coincidan sin depender del idioma de la base de datos.
            migrationBuilder.Sql($"""
                INSERT INTO companies (id, name, name_normalized, status, uses_platform_assistant, created_at)
                VALUES ('{DefaultCompanyId}', 'Demo', 'demo', 'Active', true, now());
                """);

            migrationBuilder.Sql("ALTER TABLE users ADD COLUMN company_id uuid NULL;");
            migrationBuilder.Sql("ALTER TABLE users ADD COLUMN must_change_password boolean NOT NULL DEFAULT false;");
            migrationBuilder.Sql($"UPDATE users SET company_id = '{DefaultCompanyId}';");

            // También los fichajes huérfanos: comprobar si los hay es cosa de la migración B.
            migrationBuilder.Sql("ALTER TABLE time_entries ADD COLUMN company_id uuid NULL;");
            migrationBuilder.Sql($"UPDATE time_entries SET company_id = '{DefaultCompanyId}';");

            migrationBuilder.Sql("ALTER TABLE assistant_settings ADD COLUMN company_id uuid NULL;");
            migrationBuilder.Sql($"UPDATE assistant_settings SET company_id = '{DefaultCompanyId}';");

            // Sin UPDATE: el DEFAULT constante rellena las filas existentes y las que escriba el código anterior.
            migrationBuilder.Sql($"ALTER TABLE assistant_settings_audit ADD COLUMN company_id uuid NOT NULL DEFAULT '{DefaultCompanyId}';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // DROP COLUMN no dispara los triggers de fila de la auditoría ni toca sus triggers.
            migrationBuilder.Sql("ALTER TABLE assistant_settings_audit DROP COLUMN company_id;");
            migrationBuilder.Sql("ALTER TABLE assistant_settings DROP COLUMN company_id;");
            migrationBuilder.Sql("ALTER TABLE time_entries DROP COLUMN company_id;");
            migrationBuilder.Sql("ALTER TABLE users DROP COLUMN must_change_password;");
            migrationBuilder.Sql("ALTER TABLE users DROP COLUMN company_id;");
            migrationBuilder.Sql("DROP TABLE companies;");
        }
    }
}
