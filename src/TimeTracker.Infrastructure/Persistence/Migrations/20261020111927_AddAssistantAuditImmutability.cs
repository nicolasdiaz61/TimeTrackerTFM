using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeTracker.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Auditoría de solo inserción también en la base de datos (DD-9). Se eligió un trigger y no un REVOKE porque este último
    /// no frena al propietario de la tabla, que es el rol con el que migra y opera la aplicación; el trigger sí.
    /// Solo toca assistant_settings_audit.
    /// </summary>
    public partial class AddAssistantAuditImmutability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION assistant_settings_audit_append_only() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'La tabla assistant_settings_audit solo admite inserción.' USING ERRCODE = 'raise_exception';
                END;
                $$;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER trg_assistant_settings_audit_no_update_delete
                BEFORE UPDATE OR DELETE ON assistant_settings_audit
                FOR EACH ROW EXECUTE FUNCTION assistant_settings_audit_append_only();
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER trg_assistant_settings_audit_no_truncate
                BEFORE TRUNCATE ON assistant_settings_audit
                FOR EACH STATEMENT EXECUTE FUNCTION assistant_settings_audit_append_only();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER trg_assistant_settings_audit_no_truncate ON assistant_settings_audit;");
            migrationBuilder.Sql("DROP TRIGGER trg_assistant_settings_audit_no_update_delete ON assistant_settings_audit;");
            migrationBuilder.Sql("DROP FUNCTION assistant_settings_audit_append_only();");
        }
    }
}
