using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;

namespace TimeTracker.Api.Configuration;

public static class CorsServiceCollectionExtensions
{
    public static IServiceCollection AddRestrictiveCors(this IServiceCollection services)
    {
        services.AddSingleton<IValidateOptions<ApiCorsOptions>, ApiCorsOptionsValidator>();
        services.AddOptions<ApiCorsOptions>()
            .BindConfiguration(ApiCorsOptions.SectionName)
            .ValidateOnStart();

        // Política por defecto solo con los orígenes configurados; sin credenciales ni comodines.
        services.AddCors();
        services.AddOptions<CorsOptions>().Configure<IOptions<ApiCorsOptions>>((cors, apiCors) =>
            cors.AddDefaultPolicy(policy => policy
                .WithOrigins([.. apiCors.Value.AllowedOrigins])
                .AllowAnyHeader()
                .AllowAnyMethod()));

        return services;
    }
}
