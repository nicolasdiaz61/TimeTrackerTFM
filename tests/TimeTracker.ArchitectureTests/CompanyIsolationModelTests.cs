using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using TimeTracker.Application.Identity;
using TimeTracker.Infrastructure.Persistence;

namespace TimeTracker.ArchitectureTests;

/// <summary>
/// TC-5 (DD-8, spec solution-architecture «Todas las entidades con company_id tienen filtro»): todo tipo del modelo con
/// <c>company_id</c> lleva filtro global de consulta. La lista de excepciones arranca vacía; añadir una exige revisar la decisión.
/// </summary>
public class CompanyIsolationModelTests
{
    private const string CompanyColumn = "company_id";

    // Tipos con company_id que, por decisión revisada, no llevan filtro. Vacía: `Company` no tiene la columna.
    private static readonly string[] Exceptions = [];

    [Fact]
    public void EveryEntityWithCompanyIdHasAGlobalQueryFilter()
    {
        using var context = new TimeTrackerDbContext(
            new DbContextOptionsBuilder<TimeTrackerDbContext>().UseNpgsql("Host=localhost;Database=ignored").Options,
            new NoCompany());

        var withoutFilter = EntitiesWithCompanyIdButNoFilter(context.Model);

        Assert.True(withoutFilter.Count == 0, $"Tipos con company_id sin filtro global (DD-8): {string.Join(", ", withoutFilter)}");
    }

    [Fact]
    public void TheModelHasTheFourCompanyScopedTables_AndCompaniesIsNotOneOfThem()
    {
        using var context = new TimeTrackerDbContext(
            new DbContextOptionsBuilder<TimeTrackerDbContext>().UseNpgsql("Host=localhost;Database=ignored").Options,
            new NoCompany());

        var scoped = context.Model.GetEntityTypes().Where(HasCompanyColumn).Select(type => type.GetTableName()).Order().ToList();

        Assert.Equal(["assistant_settings", "assistant_settings_audit", "time_entries", "users"], scoped);
        Assert.Empty(context.Model.FindEntityType(typeof(TimeTracker.Domain.Companies.Company))!.GetDeclaredQueryFilters());
    }

    [Fact]
    public void TheExceptionListStartsEmpty() => Assert.Empty(Exceptions);

    [Fact]
    public void AnEntityWithCompanyIdAndNoFilter_MakesTheCheckFail()
    {
        // Comprobación de la propia prueba: un tipo temporal con company_id sin filtro se detecta (y el que sí lo lleva, no).
        using var context = new UnfilteredContext(new DbContextOptionsBuilder<UnfilteredContext>().UseNpgsql("Host=localhost;Database=ignored").Options);

        var withoutFilter = EntitiesWithCompanyIdButNoFilter(context.Model);

        Assert.Equal([nameof(Unfiltered)], withoutFilter.Select(name => name.Split('+')[^1]));
    }

    private static List<string> EntitiesWithCompanyIdButNoFilter(IModel model)
        => model.GetEntityTypes()
            .Where(HasCompanyColumn)
            .Where(type => !Exceptions.Contains(type.Name) && !type.GetDeclaredQueryFilters().Any())
            .Select(type => type.Name)
            .ToList();

    private static bool HasCompanyColumn(IEntityType type)
        => type.GetProperties().Any(property => property.GetColumnName() == CompanyColumn);

    private sealed class NoCompany : ICompanyContext
    {
        public Guid? CompanyId => null;
    }

    private sealed class Unfiltered
    {
        public Guid Id { get; set; }

        public Guid CompanyId { get; set; }
    }

    private sealed class Filtered
    {
        public Guid Id { get; set; }

        public Guid CompanyId { get; set; }
    }

    private sealed class UnfilteredContext(DbContextOptions<UnfilteredContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Unfiltered>(entity => entity.Property(row => row.CompanyId).HasColumnName(CompanyColumn));
            modelBuilder.Entity<Filtered>(entity =>
            {
                entity.Property(row => row.CompanyId).HasColumnName(CompanyColumn);
                entity.HasQueryFilter(row => row.CompanyId == Guid.Empty);
            });
        }
    }
}
