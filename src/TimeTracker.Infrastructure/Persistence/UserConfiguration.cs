using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using TimeTracker.Domain.Users;

namespace TimeTracker.Infrastructure.Persistence;

internal sealed class UserConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> builder)
    {
        // La FK usa NO ACTION (no RESTRICT): PostgreSQL rechaza el borrado con 23503 en vez de 23001; protege igual.
        // Las CHECK son defensa en profundidad: las reglas viven en Domain. El rol se guarda como texto.
        builder.ToTable("users", table =>
        {
            table.HasCheckConstraint("ck_users_role", "role IN ('Employee', 'Manager')");
            table.HasCheckConstraint("ck_users_manager_not_self", "manager_id IS NULL OR manager_id <> id");
            table.HasCheckConstraint("ck_users_email_lowercase", "email = lower(email)");
        });

        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(user => user.Email).HasColumnName("email").HasMaxLength(UserAccount.MaxEmailLength).IsRequired();
        builder.Property(user => user.DisplayName).HasColumnName("display_name").HasMaxLength(UserAccount.MaxDisplayNameLength).IsRequired();
        builder.Property(user => user.PasswordHash).HasColumnName("password_hash").IsRequired();
        builder.Property(user => user.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(user => user.ManagerId).HasColumnName("manager_id");

        builder.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(user => user.ManagerId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_users_users_manager_id");

        builder.HasIndex(user => user.Email).HasDatabaseName("ux_users_email").IsUnique();
        builder.HasIndex(user => user.ManagerId).HasDatabaseName("ix_users_manager_id");
    }
}
