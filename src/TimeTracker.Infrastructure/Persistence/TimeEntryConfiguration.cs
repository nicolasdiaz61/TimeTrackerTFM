using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using TimeTracker.Domain.TimeTracking;

namespace TimeTracker.Infrastructure.Persistence;

internal sealed class TimeEntryConfiguration : IEntityTypeConfiguration<TimeEntry>
{
    // Nombres compartidos con la traducción del conflicto de entrada abierta (TimeEntryRepository).
    public const string OpenEntryIndexName = "ux_time_entries_employee_id_open";

    public void Configure(EntityTypeBuilder<TimeEntry> builder)
    {
        // La CHECK es defensa en profundidad: la regla vive en Domain.
        builder.ToTable("time_entries", table => table.HasCheckConstraint(
            "ck_time_entries_clock_out_after_clock_in",
            "clock_out_at IS NULL OR clock_out_at >= clock_in_at"));

        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(entry => entry.EmployeeId).HasColumnName("employee_id").IsRequired();
        builder.Property(entry => entry.ClockInAt).HasColumnName("clock_in_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(entry => entry.ClockOutAt).HasColumnName("clock_out_at").HasColumnType("timestamp with time zone");

        builder.HasIndex(entry => new { entry.EmployeeId, entry.ClockInAt })
            .HasDatabaseName("ix_time_entries_employee_id_clock_in_at")
            .IsDescending(false, true);
        builder.HasIndex(entry => entry.EmployeeId)
            .HasDatabaseName(OpenEntryIndexName)
            .IsUnique()
            .HasFilter("clock_out_at IS NULL");
    }
}
