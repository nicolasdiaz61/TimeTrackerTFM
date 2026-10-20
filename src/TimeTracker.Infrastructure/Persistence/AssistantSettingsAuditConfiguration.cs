using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TimeTracker.Infrastructure.Persistence;

internal sealed class AssistantSettingsAuditConfiguration : IEntityTypeConfiguration<AssistantSettingsAuditRecord>
{
    public void Configure(EntityTypeBuilder<AssistantSettingsAuditRecord> builder)
    {
        builder.ToTable("assistant_settings_audit", table => table.HasCheckConstraint("ck_assistant_settings_audit_action", "action IN ('Saved', 'Reset')"));

        builder.HasKey(row => row.Id);
        builder.Property(row => row.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        builder.Property(row => row.OccurredAt).HasColumnName("occurred_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(row => row.ActorId).HasColumnName("actor_id").IsRequired();
        builder.Property(row => row.Action).HasColumnName("action").HasMaxLength(16).IsRequired();
        builder.Property(row => row.CredentialChanged).HasColumnName("credential_changed").IsRequired();
        builder.Property(row => row.Previous).HasColumnName("previous").HasColumnType("jsonb");
        builder.Property(row => row.Current).HasColumnName("current").HasColumnType("jsonb");

        builder.HasIndex(row => row.OccurredAt).HasDatabaseName("ix_assistant_settings_audit_occurred_at").IsDescending(true);
    }
}
