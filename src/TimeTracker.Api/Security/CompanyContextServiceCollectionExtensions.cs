using TimeTracker.Application.Identity;
using TimeTracker.Infrastructure.Persistence;

namespace TimeTracker.Api.Security;

public static class CompanyContextServiceCollectionExtensions
{
    /// <summary>Una única instancia por ámbito sirve al puerto de lectura y a la interfaz de escritura.</summary>
    public static IServiceCollection AddCompanyContext(this IServiceCollection services)
    {
        services.AddScoped<AmbientCompanyContext>();
        services.AddScoped<ICompanyContext>(provider => provider.GetRequiredService<AmbientCompanyContext>());
        services.AddScoped<ICompanyContextSetter>(provider => provider.GetRequiredService<AmbientCompanyContext>());

        return services;
    }

    public static IApplicationBuilder UseCompanyContext(this IApplicationBuilder app) => app.UseMiddleware<CompanyContextMiddleware>();
}
