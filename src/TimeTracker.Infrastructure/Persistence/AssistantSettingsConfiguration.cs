using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TimeTracker.Infrastructure.Persistence;

internal sealed class AssistantSettingsConfiguration : IEntityTypeConfiguration<AssistantSettingsRecord>
{
    public void Configure(EntityTypeBuilder<AssistantSettingsRecord> builder)
    {
        // Las CHECK repiten los límites de Application como defensa en profundidad (DD-8).
        builder.ToTable("assistant_settings", table =>
        {
            table.HasCheckConstraint("ck_assistant_settings_singleton", "id = 1");
            table.HasCheckConstraint("ck_assistant_settings_provider", "provider IN ('None', 'Anthropic', 'OpenAI', 'Gemini', 'Ollama')");
            table.HasCheckConstraint("ck_assistant_settings_model", "char_length(model) >= 1");
            table.HasCheckConstraint("ck_assistant_settings_timeout_seconds", "timeout_seconds BETWEEN 5 AND 55");
            table.HasCheckConstraint("ck_assistant_settings_max_output_tokens", "max_output_tokens BETWEEN 64 AND 4096");
            table.HasCheckConstraint("ck_assistant_settings_effort", "effort IN ('low', 'medium', 'high')");
            table.HasCheckConstraint(
                "ck_assistant_settings_credential_all_or_none",
                "(credential_ciphertext IS NULL AND credential_nonce IS NULL AND credential_key_version IS NULL) "
                + "OR (credential_ciphertext IS NOT NULL AND credential_nonce IS NOT NULL AND credential_key_version IS NOT NULL)");
            table.HasCheckConstraint("ck_assistant_settings_credential_nonce_length", "credential_nonce IS NULL OR octet_length(credential_nonce) = 12");
        });

        builder.HasKey(row => row.Id);
        builder.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(row => row.Provider).HasColumnName("provider").HasMaxLength(16).IsRequired();
        builder.Property(row => row.Model).HasColumnName("model").HasMaxLength(100);
        builder.Property(row => row.TimeoutSeconds).HasColumnName("timeout_seconds");
        builder.Property(row => row.MaxOutputTokens).HasColumnName("max_output_tokens");
        builder.Property(row => row.Effort).HasColumnName("effort").HasMaxLength(8);
        builder.Property(row => row.CredentialCiphertext).HasColumnName("credential_ciphertext");
        builder.Property(row => row.CredentialNonce).HasColumnName("credential_nonce");
        builder.Property(row => row.CredentialKeyVersion).HasColumnName("credential_key_version");
        builder.Property(row => row.UpdatedBy).HasColumnName("updated_by").IsRequired();
        builder.Property(row => row.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").IsRequired();

        // Mismo token que time_entries: la columna de sistema xmin (sin columna propia).
        builder.Property<uint>("xmin").HasColumnName("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
    }
}
