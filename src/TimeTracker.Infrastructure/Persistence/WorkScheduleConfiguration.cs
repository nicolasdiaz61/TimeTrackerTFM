using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using TimeTracker.Domain.Companies;
using TimeTracker.Domain.WorkSchedules;

namespace TimeTracker.Infrastructure.Persistence;

internal sealed class WorkScheduleConfiguration : IEntityTypeConfiguration<WorkSchedule>
{
    // Compartidos con la traducción de conflictos del repositorio.
    public const string NameIndexName = "ux_work_schedules_company_id_name";

    public const string CompanyIdAlternateKeyName = "ux_work_schedules_company_id_id";

    public void Configure(EntityTypeBuilder<WorkSchedule> builder)
    {
        // Las CHECK son defensa en profundidad: las reglas viven en Domain.
        builder.ToTable("work_schedules", table =>
        {
            table.HasCheckConstraint("ck_work_schedules_lunch_minutes", "lunch_minutes BETWEEN 0 AND 480");
            table.HasCheckConstraint("ck_work_schedules_expected_break_minutes", "expected_break_minutes BETWEEN 0 AND 480");
            table.HasCheckConstraint("ck_work_schedules_days_mask", "days_mask BETWEEN 1 AND 127");
            table.HasCheckConstraint("ck_work_schedules_lunch_after_start", "lunch_start > start_time");
        });

        builder.HasKey(schedule => schedule.Id);
        builder.Property(schedule => schedule.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(schedule => schedule.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(schedule => schedule.Name).HasColumnName("name").HasMaxLength(WorkSchedule.MaxNameLength).IsRequired();
        builder.Property(schedule => schedule.StartTime).HasColumnName("start_time").HasColumnType("time without time zone").IsRequired();
        builder.Property(schedule => schedule.LunchStart).HasColumnName("lunch_start").HasColumnType("time without time zone").IsRequired();
        builder.Property(schedule => schedule.LunchMinutes).HasColumnName("lunch_minutes").IsRequired();
        builder.Property(schedule => schedule.ExpectedBreakMinutes).HasColumnName("expected_break_minutes").IsRequired();
        builder.Property(schedule => schedule.DaysMask).HasColumnName("days_mask").IsRequired();
        // Nula en las jornadas anteriores a la hora de salida: no hay relleno de datos (ADR 0031).
        builder.Property(schedule => schedule.EndTime).HasColumnName("end_time").HasColumnType("time without time zone");
        builder.Ignore(schedule => schedule.ExpectedWorkMinutes);
        builder.Ignore(schedule => schedule.Days);

        // Destino de la clave foránea compuesta de users: una persona solo puede tener una jornada de su misma compañía.
        builder.HasAlternateKey(schedule => new { schedule.CompanyId, schedule.Id }).HasName(CompanyIdAlternateKeyName);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(schedule => schedule.CompanyId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_work_schedules_companies_company_id");

        builder.HasIndex(schedule => new { schedule.CompanyId, schedule.Name }).HasDatabaseName(NameIndexName).IsUnique();
    }
}
