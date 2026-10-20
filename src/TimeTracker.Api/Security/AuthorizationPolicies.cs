using TimeTracker.Domain.Users;

namespace TimeTracker.Api.Security;

/// <summary>Políticas de autorización con nombre: un solo registro, compartido por los endpoints que las exigen.</summary>
public static class AuthorizationPolicies
{
    public const string Manager = "Manager";

    public static IServiceCollection AddAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(Manager, policy => policy.RequireRole(nameof(UserRole.Manager)));

        return services;
    }
}
