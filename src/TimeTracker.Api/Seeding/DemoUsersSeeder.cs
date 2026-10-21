using Microsoft.Extensions.Options;

using TimeTracker.Application.Authentication;
using TimeTracker.Application.Companies;
using TimeTracker.Application.Users;
using TimeTracker.Domain.Companies;
using TimeTracker.Domain.Users;
using TimeTracker.Infrastructure.Persistence;

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

        // La siembra actúa como la compañía por defecto (DD-22).
        await using var scope = services.GetRequiredService<ICompanyScopeFactory>().CreateScope(Company.DefaultId);
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var companyId = Company.DefaultId;
        await scope.ServiceProvider.GetRequiredService<ICompanyRepository>().EnsureDefaultAsync(cancellationToken);
        var existingByEmail = await FindExistingAsync(companyId);
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
            if (existingByEmail.TryGetValue(demo.Email, out var existing))
            {
                return (existing, false);
            }

            var user = UserAccount.Create(
                Guid.CreateVersion7(clock.GetUtcNow()),
                companyId,
                demo.Email,
                demo.DisplayName,
                hasher.Hash(options.Value.DemoPassword!),
                demo.Role,
                managerId);
            await users.AddAsync(user, cancellationToken);
            return (user, true);
        }

        // Se comprueban todos antes de crear ninguno: un correo demo de otra compañía detiene el arranque sin sembrar nada.
        async Task<Dictionary<string, UserAccount>> FindExistingAsync(Guid expectedCompanyId)
        {
            var found = new Dictionary<string, UserAccount>();
            foreach (var demo in DemoUsers.All)
            {
                var user = await users.FindByEmailAsync(UserAccount.NormalizeEmail(demo.Email), cancellationToken);
                if (user is null)
                {
                    continue;
                }

                if (user.CompanyId != expectedCompanyId)
                {
                    throw new InvalidOperationException(
                        "Seed:DemoUsers: un usuario de demostración ya existe en otra compañía; el arranque se detiene sin modificar nada.");
                }

                found[demo.Email] = user;
            }

            return found;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // Solo recuentos: ni correos ni la contraseña (A09).
    [LoggerMessage(Level = LogLevel.Information, Message = "Usuarios de demostración sembrados: {Created} creados y {Existing} ya existentes.")]
    private static partial void LogSeeded(ILogger logger, int created, int existing);
}
