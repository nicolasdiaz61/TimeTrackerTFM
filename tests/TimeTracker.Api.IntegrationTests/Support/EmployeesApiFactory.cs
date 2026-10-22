using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using TimeTracker.Infrastructure.Persistence;

namespace TimeTracker.Api.IntegrationTests.Support;

/// <summary>
/// API con PostgreSQL real para el alta de personas, la regeneración y el cambio de contraseña. Captura todo lo que registra la
/// aplicación (para comprobar que ninguna contraseña llega a los registros) y admite ajustes de configuración y un interceptor de
/// EF Core (para provocar una carrera entre la lectura y el guardado).
/// </summary>
internal sealed class EmployeesApiFactory(
    string connectionString,
    IReadOnlyDictionary<string, string?>? settings = null,
    IInterceptor? interceptor = null) : ApiFactory
{
    public CapturingLoggerProvider Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("ConnectionStrings:Default", connectionString);
        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureLogging(logging => logging.AddProvider(Logs));
        if (interceptor is not null)
        {
            builder.ConfigureTestServices(services => services.ConfigureDbContext<TimeTrackerDbContext>(options => options.AddInterceptors(interceptor)));
        }
    }
}

/// <summary>Ejecuta una sentencia SQL desde otra conexión justo antes del primer guardado del contexto de la API: otra operación se adelanta.</summary>
internal sealed class OneShotRacingInterceptor(string connectionString, string sql) : SaveChangesInterceptor
{
    private int _fired;

    public bool Fired => Volatile.Read(ref _fired) == 1;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _fired, 1) == 0)
        {
            await MigrationDatabase.ExecuteAsync(connectionString, sql);
        }

        return result;
    }
}
