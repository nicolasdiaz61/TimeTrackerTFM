using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using TimeTracker.Domain.Companies;

namespace TimeTracker.Infrastructure.Persistence;

internal sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    // Nombre compartido con la traducción del conflicto de la compañía por defecto (CompanyRepository).
    public const string PrimaryKeyName = "PK_companies";

    public void Configure(EntityTypeBuilder<Company> builder)
    {
        // Las CHECK son defensa en profundidad: las reglas viven en Domain. El estado se guarda como texto.
        builder.ToTable("companies", table =>
        {
            table.HasCheckConstraint("ck_companies_name_length", "char_length(btrim(name)) BETWEEN 1 AND 100");
            table.HasCheckConstraint("ck_companies_status", "status IN ('Active', 'Suspended')");
        });

        builder.HasKey(company => company.Id).HasName(PrimaryKeyName);
        builder.Property(company => company.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(company => company.Name).HasColumnName("name").HasMaxLength(Company.MaxNameLength).IsRequired();
        builder.Property(company => company.NameNormalized).HasColumnName("name_normalized").HasMaxLength(Company.MaxNameLength).IsRequired();
        builder.Property(company => company.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16).HasDefaultValue(CompanyStatus.Active).IsRequired();
        builder.Property(company => company.UsesPlatformAssistant).HasColumnName("uses_platform_assistant").HasDefaultValue(false).IsRequired();
        builder.Property(company => company.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();

        builder.HasIndex(company => company.NameNormalized).HasDatabaseName("ux_companies_name_normalized").IsUnique();
    }
}
