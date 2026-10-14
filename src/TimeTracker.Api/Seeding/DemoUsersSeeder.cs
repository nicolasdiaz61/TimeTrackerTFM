using Microsoft.Extensions.Options;

using TimeTracker.Application.Authentication;
using TimeTracker.Application.Users;
using TimeTracker.Domain.Users;

namespace TimeTracker.Api.Seeding;

/// <summary>
/// Crea los usuarios de demostración al arrancar si <c>Seed:DemoUsers</c> es <c>true</c>. Va en <c>StartAsync</c>:
/// todas las <c>StartingAsync</c> (la migración) terminan antes que cualquier <c>StartAsync</c>, y los servicios
/// hospedados arrancan antes de que Kestrel escuche. Con la base inaccesible el arranque falla (DD-22).
/// </summary>
public sealed partial class DemoUsersSeeder(
    IOptions<SeedOptions> options,
    IServiceProvider services,
    TimeProvider clock,
    ILogger<DemoUsersSeeder> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.DemoUsers)
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var created = 0;

        // Idempotente por correo: un usuario existente no se modifica y los vínculos usan el id vigente de Marta.
        var (manager, managerCreated) = await EnsureAsync(DemoUsers.Manager, managerId: null);
        created += managerCreated ? 1 : 0;
        foreach (var report in DemoUsers.Reports)
        {
            created += (await EnsureAsync(report, manager.Id)).Created ? 1 : 0;
        }

        created += (await EnsureAsync(DemoUsers.Unassigned, managerId: null)).Created ? 1 : 0;
        LogSeeded(logger, created, DemoUsers.Reports.Count + 2 - created);

        async Task<(UserAccount User, bool Created)> EnsureAsync(DemoUser demo, Guid? managerId)
        {
            var existing = await users.FindByEmailAsync(UserAccount.NormalizeEmail(demo.Email), cancellationToken);
            if (existing is not null)
            {
                return (existing, false);
            }

            var user = UserAccount.Create(
                Guid.CreateVersion7(clock.GetUtcNow()),
                demo.Email,
                demo.DisplayName,
                hasher.Hash(options.Value.DemoPassword!),
                demo.Role,
                managerId);
            await users.AddAsync(user, cancellationToken);
            return (user, true);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // Solo recuentos: ni correos ni la contraseña (A09).
    [LoggerMessage(Level = LogLevel.Information, Message = "Usuarios de demostración sembrados: {Created} creados y {Existing} ya existentes.")]
    private static partial void LogSeeded(ILogger logger, int created, int existing);
}
