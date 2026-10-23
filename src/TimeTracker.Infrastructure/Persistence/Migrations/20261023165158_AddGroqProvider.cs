using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGroqProvider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_assistant_settings_provider",
                table: "assistant_settings");

            migrationBuilder.AddCheckConstraint(
                name: "ck_assistant_settings_provider",
                table: "assistant_settings",
                sql: "provider IN ('None', 'Anthropic', 'OpenAI', 'Gemini', 'Groq', 'Ollama')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_assistant_settings_provider",
                table: "assistant_settings");

            migrationBuilder.AddCheckConstraint(
                name: "ck_assistant_settings_provider",
                table: "assistant_settings",
                sql: "provider IN ('None', 'Anthropic', 'OpenAI', 'Gemini', 'Ollama')");
        }
    }
}
