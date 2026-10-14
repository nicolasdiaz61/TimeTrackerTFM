using Microsoft.Extensions.Options;

using TimeTracker.Application.DemoData;

namespace TimeTracker.Api.Seeding;

public static class SeedingServiceCollectionExtensions
{
    public static IServiceCollection AddDemoSeeding(this IServiceCollection services)
    {
        services.AddSingleton<IValidateOptions<SeedOptions>, SeedOptionsValidator>();
        services.AddOptions<SeedOptions>()
            .BindConfiguration(SeedOptions.SectionName)
            .ValidateOnStart();

        // Tras AddDatabaseConfiguration; da igual el orden de registro: la migración va en StartingAsync (V16).
        services.AddHostedService<DemoUsersSeeder>();

        // Después de DemoUsersSeeder: los servicios hospedados arrancan en orden de registro y necesita a los usuarios.
        services.AddScoped<SeedDemoTimeEntriesUseCase>();
        services.AddHostedService<DemoDataSeeder>();

        return services;
    }
}
