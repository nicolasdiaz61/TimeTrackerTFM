using Microsoft.Extensions.Options;

using TimeTracker.Application.Companies;

namespace TimeTracker.Api.Registration;

public static class RegistrationServiceCollectionExtensions
{
    public static IServiceCollection AddRegistration(this IServiceCollection services)
    {
        services.AddSingleton<IValidateOptions<RegistrationOptions>, RegistrationOptionsValidator>();
        services.AddOptions<RegistrationOptions>()
            .BindConfiguration(RegistrationOptions.SectionName)
            .ValidateOnStart();

        services.AddScoped<RegisterCompanyUseCase>();
        services.AddHostedService<DemoUsersStartupGuard>();

        // Los códigos se resumen una sola vez; con el registro cerrado nadie resuelve el validador y los códigos no se leen.
        services.AddSingleton<IInviteCodeValidator>(provider => new InviteCodeValidator(provider.GetRequiredService<IOptions<RegistrationOptions>>().Value.ParseInviteCodes()));

        return services;
    }
}
