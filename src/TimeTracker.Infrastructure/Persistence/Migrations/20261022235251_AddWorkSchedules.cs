using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "work_schedule_id",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "work_schedules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    lunch_start = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    lunch_minutes = table.Column<int>(type: "integer", nullable: false),
                    expected_break_minutes = table.Column<int>(type: "integer", nullable: false),
                    days_mask = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_schedules", x => x.id);
                    table.UniqueConstraint("ux_work_schedules_company_id_id", x => new { x.company_id, x.id });
                    table.CheckConstraint("ck_work_schedules_days_mask", "days_mask BETWEEN 1 AND 127");
                    table.CheckConstraint("ck_work_schedules_expected_break_minutes", "expected_break_minutes BETWEEN 0 AND 480");
                    table.CheckConstraint("ck_work_schedules_lunch_after_start", "lunch_start > start_time");
                    table.CheckConstraint("ck_work_schedules_lunch_minutes", "lunch_minutes BETWEEN 0 AND 480");
                    table.ForeignKey(
                        name: "fk_work_schedules_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_users_company_id_work_schedule_id",
                table: "users",
                columns: new[] { "company_id", "work_schedule_id" });

            migrationBuilder.CreateIndex(
                name: "ux_work_schedules_company_id_name",
                table: "work_schedules",
                columns: new[] { "company_id", "name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_users_work_schedules_company_id_work_schedule_id",
                table: "users",
                columns: new[] { "company_id", "work_schedule_id" },
                principalTable: "work_schedules",
                principalColumns: new[] { "company_id", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_users_work_schedules_company_id_work_schedule_id",
                table: "users");

            migrationBuilder.DropTable(
                name: "work_schedules");

            migrationBuilder.DropIndex(
                name: "ix_users_company_id_work_schedule_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "work_schedule_id",
                table: "users");
        }
    }
}
