using Microsoft.Extensions.Options;

using TimeTracker.Application.Companies;

namespace TimeTracker.Api.Companies;

public static class CompanyServiceCollectionExtensions
{
    /// <summary>Registra y valida al arrancar los topes por compañía del entorno (<c>Company:MaxEmployees</c>).</summary>
    public static IServiceCollection AddCompanyLimits(this IServiceCollection services)
    {
        services.AddSingleton<IValidateOptions<CompanyOptions>, CompanyOptionsValidator>();
        services.AddOptions<CompanyOptions>()
            .Configure<IConfiguration>(CompanyOptions.Bind)
            .ValidateOnStart();
        services.AddSingleton<ICompanyLimits, ConfiguredCompanyLimits>();

        return services;
    }
}
