using Microsoft.Extensions.Options;

using TimeTracker.Api.Seeding;
using TimeTracker.Application.Users;
using TimeTracker.Domain.Companies;
using TimeTracker.Infrastructure.Persistence;

namespace TimeTracker.Api.Registration;

/// <summary>
/// Red de seguridad del registro abierto (DD-22): en <c>Production</c> impide el arranque si la compañía por defecto conserva usuarios
/// de demostración, cuya contraseña es pública. Apagar la siembra no los retira. Va en <c>StartAsync</c>: las migraciones corren antes
/// (<c>StartingAsync</c>) y los servicios alojados arrancan antes de que Kestrel atienda ninguna petición. Solo se mira la compañía por
/// defecto, que es donde la siembra crea esos usuarios. El mensaje no incluye correos, contraseñas ni códigos.
/// </summary>
public sealed class DemoUsersStartupGuard(
    IOptions<RegistrationOptions> options,
    IHostEnvironment environment,
    ICompanyScopeFactory scopes) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled || !environment.IsProduction())
        {
            return;
        }

        await using var scope = scopes.CreateScope(Company.DefaultId);
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        if (await users.ExistsWithEmailDomainAsync(DemoUsers.EmailDomain, cancellationToken))
        {
            throw new InvalidOperationException(
                "Registration:Enabled es true pero la compañía por defecto conserva usuarios de demostración, cuya contraseña es pública. " +
                "Retire los usuarios de demostración antes de abrir el registro.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
