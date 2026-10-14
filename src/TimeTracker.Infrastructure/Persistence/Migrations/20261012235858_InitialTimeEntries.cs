using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialTimeEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "time_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    clock_in_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    clock_out_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_time_entries", x => x.id);
                    table.CheckConstraint("ck_time_entries_clock_out_after_clock_in", "clock_out_at IS NULL OR clock_out_at >= clock_in_at");
                });

            migrationBuilder.CreateIndex(
                name: "ix_time_entries_employee_id_clock_in_at",
                table: "time_entries",
                columns: new[] { "employee_id", "clock_in_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ux_time_entries_employee_id_open",
                table: "time_entries",
                column: "employee_id",
                unique: true,
                filter: "clock_out_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "time_entries");
        }
    }
}
