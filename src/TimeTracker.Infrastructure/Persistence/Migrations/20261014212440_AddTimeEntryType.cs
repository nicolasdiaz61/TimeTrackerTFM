using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTimeEntryType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Las filas existentes pasan a ser intervalos de trabajo ('Work'); el valor por defecto se conserva en la base de datos
            // (ADR 0014). La propiedad sombra xmin (token de concurrencia) usa la columna de sistema de PostgreSQL: no se crea nada.
            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "time_entries",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Work");

            migrationBuilder.AddCheckConstraint(
                name: "ck_time_entries_type",
                table: "time_entries",
                sql: "type IN ('Work', 'Break', 'MealBreak')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_time_entries_type",
                table: "time_entries");

            migrationBuilder.DropColumn(
                name: "type",
                table: "time_entries");
        }
    }
}
