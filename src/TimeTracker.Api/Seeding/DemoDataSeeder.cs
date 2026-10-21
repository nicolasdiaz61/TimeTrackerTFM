using Microsoft.Extensions.Options;

using TimeTracker.Application.DemoData;
using TimeTracker.Application.Users;
using TimeTracker.Domain.Users;

namespace TimeTracker.Api.Seeding;

/// <summary>
/// Siembra un mes de fichajes de demostración si <c>Seed:DemoData</c> es <c>true</c>. Debe registrarse después de
/// <see cref="DemoUsersSeeder"/>: los servicios hospedados arrancan en orden de registro y los usuarios han de existir ya.
/// Un fallo (usuario ausente, base inaccesible) impide el arranque.
/// </summary>
public sealed partial class DemoDataSeeder(
    IOptions<SeedOptions> options,
    IServiceProvider services,
    ILogger<DemoDataSeeder> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.DemoData)
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var employees = new List<DemoEmployee>();
        foreach (var demo in DemoUsers.All)
        {
            var user = await users.FindByEmailAsync(UserAccount.NormalizeEmail(demo.Email), cancellationToken)
                ?? throw new InvalidOperationException("Seed:DemoData: falta un usuario de demostración; los fichajes demo se siembran tras Seed:DemoUsers.");
            employees.Add(new DemoEmployee(user.Id, user.CompanyId, demo.Schedule));
        }

        var result = await scope.ServiceProvider.GetRequiredService<SeedDemoTimeEntriesUseCase>().ExecuteAsync(employees, cancellationToken);
        LogSeeded(logger, result.CreatedDays, result.SkippedDays, result.CreatedEntries);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // Solo recuentos: ni correos ni identificadores (A09).
    [LoggerMessage(Level = LogLevel.Information, Message = "Fichajes de demostración sembrados: {CreatedDays} días creados, {SkippedDays} omitidos (ya tenían fichajes) y {CreatedEntries} intervalos.")]
    private static partial void LogSeeded(ILogger logger, int createdDays, int skippedDays, int createdEntries);
}
