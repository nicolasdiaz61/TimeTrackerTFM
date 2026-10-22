using System.Linq.Expressions;

using Microsoft.EntityFrameworkCore;

using TimeTracker.Application.Identity;
using TimeTracker.Domain.Companies;
using TimeTracker.Domain.TimeTracking;
using TimeTracker.Domain.Users;

namespace TimeTracker.Infrastructure.Persistence;

/// <summary>
/// Contexto de EF Core. Toda entidad con <c>CompanyId</c> lleva un filtro global que falla cerrado (DD-8): sin compañía en el
/// contexto, o con una compañía vacía, no devuelve filas y no lanza.
/// </summary>
public sealed class TimeTrackerDbContext : DbContext
{
    internal const string CompanyIdProperty = "CompanyId";

    private readonly ICompanyContext? _companyContext;

    public TimeTrackerDbContext(DbContextOptions<TimeTrackerDbContext> options, ICompanyContext companyContext)
        : base(options)
        => _companyContext = companyContext;

    /// <summary>Solo para <c>dotnet-ef</c>: sin compañía en el contexto (el contexto de diseño no consulta datos).</summary>
    internal TimeTrackerDbContext(DbContextOptions<TimeTrackerDbContext> options)
        : base(options)
    {
    }

    public DbSet<Company> Companies => Set<Company>();

    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();

    public DbSet<UserAccount> Users => Set<UserAccount>();

    /// <summary>Compañía del contexto; <see cref="Guid.Empty"/> si no hay ninguna, que no coincide con ninguna fila (la FK a <c>companies</c> lo impide).</summary>
    internal Guid CurrentCompanyId => _companyContext?.CompanyId ?? Guid.Empty;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TimeTrackerDbContext).Assembly);
        ApplyCompanyFilter(modelBuilder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnforceCompanyScope();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnforceCompanyScope();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    // Guarda de escritura (DD-7): el filtro solo acota lecturas; esta comprobación cierra `Update(entidadDesconectada)`, `Remove` y el
    // cambio de CompanyId. Sin compañía en el contexto (Guid.Empty) no se permite ninguna escritura de entidades con compañía.
    private void EnforceCompanyScope()
    {
        foreach (var entry in ChangeTracker.Entries().Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            if (entry.Metadata.FindProperty(CompanyIdProperty) is null)
            {
                continue;
            }

            var company = entry.Property(CompanyIdProperty);
            var current = (Guid)company.CurrentValue!;
            var changed = entry.State != EntityState.Added && (Guid)company.OriginalValue! != current;
            if (CurrentCompanyId == Guid.Empty || current != CurrentCompanyId || changed)
            {
                throw new CompanyScopeViolationException(entry.Metadata.ClrType.Name);
            }
        }
    }

    // Un único bucle por convención: una entidad nueva con CompanyId hereda el filtro sin tocar su configuración. `Company` no lo tiene.
    private void ApplyCompanyFilter(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(type => type.FindProperty(CompanyIdProperty) is not null))
        {
            var entity = Expression.Parameter(entityType.ClrType, "entity");
            var entityCompany = Expression.Call(typeof(EF), nameof(EF.Property), [typeof(Guid)], entity, Expression.Constant(CompanyIdProperty));
            // La referencia a esta instancia hace que EF evalúe el filtro con el contexto que ejecuta cada consulta (V-2).
            var contextCompany = Expression.Property(Expression.Constant(this), nameof(CurrentCompanyId));
            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(Expression.Lambda(Expression.Equal(entityCompany, contextCompany), entity));
        }
    }
}
