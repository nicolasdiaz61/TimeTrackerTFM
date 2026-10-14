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
        // Las CHECK son defensa en profundidad: las reglas viven en Domain.
        builder.ToTable("time_entries", table =>
        {
            table.HasCheckConstraint(
                "ck_time_entries_clock_out_after_clock_in",
                "clock_out_at IS NULL OR clock_out_at >= clock_in_at");
            table.HasCheckConstraint("ck_time_entries_type", "type IN ('Work', 'Break', 'MealBreak')");
        });

        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(entry => entry.EmployeeId).HasColumnName("employee_id").IsRequired();

        // Texto (nombre del miembro) y no número: legible y estable si se reordena o amplía la enumeración (ADR 0014).
        // La base de datos añade además DEFAULT 'Work' (migración AddTimeEntryType) para las filas anteriores a este campo.
        builder.Property(entry => entry.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(entry => entry.ClockInAt).HasColumnName("clock_in_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(entry => entry.ClockOutAt).HasColumnName("clock_out_at").HasColumnType("timestamp with time zone");

        // Token de concurrencia optimista: la columna de sistema xmin de PostgreSQL (sin columna propia ni cambios en Domain).
        builder.Property<uint>("xmin").HasColumnName("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();

        builder.HasIndex(entry => new { entry.EmployeeId, entry.ClockInAt })
            .HasDatabaseName("ix_time_entries_employee_id_clock_in_at")
            .IsDescending(false, true);
        builder.HasIndex(entry => entry.EmployeeId)
            .HasDatabaseName(OpenEntryIndexName)
            .IsUnique()
            .HasFilter("clock_out_at IS NULL");
    }
}
