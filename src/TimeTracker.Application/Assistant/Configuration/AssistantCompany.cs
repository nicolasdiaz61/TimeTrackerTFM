using TimeTracker.Application.Companies;
using TimeTracker.Application.Identity;

namespace TimeTracker.Application.Assistant.Configuration;

/// <summary>
/// La compañía del token y si usa los valores de plataforma del asistente (<c>uses_platform_assistant</c>, la compañía demo).
/// La compañía sale siempre de <see cref="ICurrentUser"/>; ningún caso de uso la recibe como parámetro (DD-13).
/// </summary>
internal sealed record AssistantCompany(Guid Id, bool UsesPlatformAssistant)
{
    public static async Task<AssistantCompany> ResolveAsync(ICompanyRepository companies, ICurrentUser currentUser, CancellationToken cancellationToken)
    {
        var id = currentUser.CompanyId;
        var info = await companies.FindLoginInfoAsync(id, cancellationToken);
        return new AssistantCompany(id, info?.UsesPlatformAssistant ?? false);
    }

    /// <summary>El entorno tal como lo puede usar esta compañía (la que no es de plataforma no hereda modelo, esfuerzo ni clave).</summary>
    public AssistantEnvironmentDefaults EnvironmentOf(IAssistantEnvironment environment) => environment.Current.ForCompany(UsesPlatformAssistant);
}
