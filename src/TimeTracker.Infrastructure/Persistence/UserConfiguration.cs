using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using TimeTracker.Domain.Companies;
using TimeTracker.Domain.Users;

namespace TimeTracker.Infrastructure.Persistence;

internal sealed class UserConfiguration : IEntityTypeConfiguration<UserAccount>
{
    // Compartido con la traducción del conflicto del autorregistro (CompanyRegistrationStore).
    public const string EmailIndexName = "ux_users_email";

    // Compartido con la traducción del alta con tope (UserRepository): un responsable de otra compañía incumple esta clave.
    public const string ManagerForeignKeyName = "fk_users_users_company_id_manager_id";

    public void Configure(EntityTypeBuilder<UserAccount> builder)
    {
        // Las FK usan NO ACTION (no RESTRICT): PostgreSQL rechaza el borrado con 23503 en vez de 23001; protege igual.
        // Las CHECK son defensa en profundidad: las reglas viven en Domain. El rol se guarda como texto.
        builder.ToTable("users", table =>
        {
            table.HasCheckConstraint("ck_users_role", "role IN ('Employee', 'Manager')");
            table.HasCheckConstraint("ck_users_manager_not_self", "manager_id IS NULL OR manager_id <> id");
            table.HasCheckConstraint("ck_users_email_lowercase", "email = lower(email)");
        });

        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(user => user.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(user => user.Email).HasColumnName("email").HasMaxLength(UserAccount.MaxEmailLength).IsRequired();
        builder.Property(user => user.DisplayName).HasColumnName("display_name").HasMaxLength(UserAccount.MaxDisplayNameLength).IsRequired();
        builder.Property(user => user.PasswordHash).HasColumnName("password_hash").IsRequired();
        builder.Property(user => user.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(user => user.ManagerId).HasColumnName("manager_id");
        builder.Property(user => user.MustChangePassword).HasColumnName("must_change_password").HasDefaultValue(false).IsRequired();

        // Token de concurrencia optimista: la columna de sistema xmin (sin columna propia). Lo usan el cambio de
        // contraseña y la regeneración de la contraseña temporal (DD-21).
        builder.Property<uint>("xmin").HasColumnName("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();

        // Destino de las claves foráneas compuestas: una persona solo puede apuntar a otra de su propia compañía.
        builder.HasAlternateKey(user => new { user.CompanyId, user.Id }).HasName("ux_users_company_id_id");

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(user => user.CompanyId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_users_companies_company_id");

        // MATCH SIMPLE (valor por defecto de PostgreSQL): con manager_id nulo no se comprueba nada.
        builder.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(user => new { user.CompanyId, user.ManagerId })
            .HasPrincipalKey(user => new { user.CompanyId, user.Id })
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName(ManagerForeignKeyName);

        builder.HasIndex(user => user.Email).HasDatabaseName(EmailIndexName).IsUnique();
        builder.HasIndex(user => new { user.CompanyId, user.ManagerId }).HasDatabaseName("ix_users_company_id_manager_id");
    }
}
