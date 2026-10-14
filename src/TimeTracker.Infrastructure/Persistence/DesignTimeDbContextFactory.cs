using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TimeTracker.Infrastructure.Persistence;

/// <summary>Solo para <c>dotnet-ef</c>: construye el contexto sin conectar ni requerir contraseña.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TimeTrackerDbContext>
{
    private const string FallbackConnectionString = "Host=localhost;Database=timetracker_design";

    public TimeTrackerDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        var options = new DbContextOptionsBuilder<TimeTrackerDbContext>()
            .UseNpgsql(string.IsNullOrWhiteSpace(connectionString) ? FallbackConnectionString : connectionString)
            .Options;

        return new TimeTrackerDbContext(options);
    }
}
