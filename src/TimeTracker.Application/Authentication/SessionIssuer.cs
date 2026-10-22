using TimeTracker.Application.Companies;
using TimeTracker.Domain.Companies;
using TimeTracker.Domain.Users;

namespace TimeTracker.Application.Authentication;

/// <summary>
/// Único camino para emitir un token: comprueba que la compañía del usuario está <see cref="CompanyStatus.Active"/> antes de
/// emitirlo (DD-6). El inicio de sesión, el cambio de contraseña y el registro pasan por aquí, de modo que ninguna renovación
/// de sesión se salta la regla.
/// </summary>
public sealed class SessionIssuer(ICompanyRepository companies, IAccessTokenIssuer issuer)
{
    /// <summary>Indica si la compañía existe y está activa; para comprobarlo antes de cambiar nada, sin emitir un token.</summary>
    public async Task<bool> IsCompanyActiveAsync(Guid companyId, CancellationToken cancellationToken)
        => await companies.FindLoginInfoAsync(companyId, cancellationToken) is { Status: CompanyStatus.Active };

    /// <summary>Devuelve <c>null</c> si la compañía no existe o no está activa: el llamador responde el mismo 401 genérico.</summary>
    public async Task<LoginResult?> IssueAsync(UserAccount user, CancellationToken cancellationToken)
    {
        var company = await companies.FindLoginInfoAsync(user.CompanyId, cancellationToken);
        if (company is not { Status: CompanyStatus.Active })
        {
            return null;
        }

        return new LoginResult(issuer.Issue(user, company.Name), UserSummary.From(user, company.Name));
    }
}
