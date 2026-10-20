using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TimeTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAssistantSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "assistant_settings",
                columns: table => new
                {
                    id = table.Column<short>(type: "smallint", nullable: false),
                    provider = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    timeout_seconds = table.Column<int>(type: "integer", nullable: true),
                    max_output_tokens = table.Column<int>(type: "integer", nullable: true),
                    effort = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    credential_ciphertext = table.Column<byte[]>(type: "bytea", nullable: true),
                    credential_nonce = table.Column<byte[]>(type: "bytea", nullable: true),
                    credential_key_version = table.Column<short>(type: "smallint", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                    // xmin es la columna de sistema de PostgreSQL (token de concurrencia): no se crea, igual que en time_entries.
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assistant_settings", x => x.id);
                    table.CheckConstraint("ck_assistant_settings_credential_all_or_none", "(credential_ciphertext IS NULL AND credential_nonce IS NULL AND credential_key_version IS NULL) OR (credential_ciphertext IS NOT NULL AND credential_nonce IS NOT NULL AND credential_key_version IS NOT NULL)");
                    table.CheckConstraint("ck_assistant_settings_credential_nonce_length", "credential_nonce IS NULL OR octet_length(credential_nonce) = 12");
                    table.CheckConstraint("ck_assistant_settings_effort", "effort IN ('low', 'medium', 'high')");
                    table.CheckConstraint("ck_assistant_settings_max_output_tokens", "max_output_tokens BETWEEN 64 AND 4096");
                    table.CheckConstraint("ck_assistant_settings_model", "char_length(model) >= 1");
                    table.CheckConstraint("ck_assistant_settings_provider", "provider IN ('None', 'Anthropic', 'OpenAI', 'Gemini', 'Ollama')");
                    table.CheckConstraint("ck_assistant_settings_singleton", "id = 1");
                    table.CheckConstraint("ck_assistant_settings_timeout_seconds", "timeout_seconds BETWEEN 5 AND 55");
                });

            migrationBuilder.CreateTable(
                name: "assistant_settings_audit",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    credential_changed = table.Column<bool>(type: "boolean", nullable: false),
                    previous = table.Column<string>(type: "jsonb", nullable: true),
                    current = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assistant_settings_audit", x => x.id);
                    table.CheckConstraint("ck_assistant_settings_audit_action", "action IN ('Saved', 'Reset')");
                });

            migrationBuilder.CreateIndex(
                name: "ix_assistant_settings_audit_occurred_at",
                table: "assistant_settings_audit",
                column: "occurred_at",
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assistant_settings");

            migrationBuilder.DropTable(
                name: "assistant_settings_audit");
        }
    }
}
