using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using TimeTracker.Application.Assistant.Configuration;
using TimeTracker.Application.DemoData;
using TimeTracker.Application.TimeTracking;
using TimeTracker.Application.Users;

namespace TimeTracker.Infrastructure.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    /// <summary>Registra el contexto de EF Core y el repositorio. La cadena de conexión ya se validó al arrancar.</summary>
    public static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        services.AddDbContext<TimeTrackerDbContext>((provider, options) =>
            options.UseNpgsql(provider.GetRequiredService<IOptions<ConnectionStringOptions>>().Value.Default));
        services.AddScoped<ITimeEntryRepository, TimeEntryRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IDemoTimeEntryStore, DemoTimeEntryStore>();
        services.AddScoped<IAssistantSettingsStore, AssistantSettingsStore>();

        return services;
    }

    /// <summary>Aplica las migraciones pendientes (nunca <c>EnsureCreated</c>). Un fallo de conexión se propaga.</summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TimeTrackerDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }
}
