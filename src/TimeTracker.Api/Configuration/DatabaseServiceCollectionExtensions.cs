using Microsoft.Extensions.Options;

using TimeTracker.Api.Persistence;
using TimeTracker.Infrastructure.Persistence;

namespace TimeTracker.Api.Configuration;

public static class DatabaseServiceCollectionExtensions
{
    public static IServiceCollection AddDatabaseConfiguration(this IServiceCollection services)
    {
        services.AddSingleton<IValidateOptions<ConnectionStringOptions>, ConnectionStringOptionsValidator>();
        services.AddOptions<ConnectionStringOptions>()
            .BindConfiguration(ConnectionStringOptions.SectionName)
            .ValidateOnStart();

        // Un valor no booleano falla al enlazar y el mensaje del binder nombra la clave.
        services.AddOptions<DatabaseOptions>()
            .BindConfiguration(DatabaseOptions.SectionName)
            .ValidateOnStart();

        services.AddHostedService<DatabaseMigrationHostedService>();

        return services;
    }
}
