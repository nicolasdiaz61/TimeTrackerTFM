using Microsoft.Extensions.Options;

using TimeTracker.Api.Configuration;
using TimeTracker.Infrastructure.Persistence;

namespace TimeTracker.Api.Persistence;

/// <summary>
/// Migra la base de datos al arrancar si la bandera lo indica. Va en <c>StartingAsync</c> porque corre tras la
/// validación de opciones y antes de que Kestrel escuche, y porque <c>WebApplicationFactory</c> y
/// <c>GetDocument</c> interceptan <c>Build()</c>: el código posterior de <c>Program.cs</c> no se ejecutaría (DD-12).
/// </summary>
public sealed partial class DatabaseMigrationHostedService(
    IOptions<DatabaseOptions> options,
    IServiceProvider services,
    ILogger<DatabaseMigrationHostedService> logger) : IHostedLifecycleService
{
    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.ApplyMigrationsOnStartup)
        {
            return;
        }

        LogApplyingMigrations(logger);
        await services.MigrateDatabaseAsync(cancellationToken);
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = "Aplicando las migraciones pendientes de la base de datos.")]
    private static partial void LogApplyingMigrations(ILogger logger);
}
