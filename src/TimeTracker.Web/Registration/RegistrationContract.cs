using TimeTracker.Web.Auth;
using TimeTracker.Web.Http;

namespace TimeTracker.Web.Registration;

// Contrato de POST /api/auth/register-company (docs/api/openapi.json). La Web no conoce los códigos ni la bandera del servidor
// y nunca envía un identificador de compañía.
public sealed record RegisterCompanyRequestDto(string CompanyName, string ManagerName, string Email, string Password, string InviteCode);

public interface IRegistrationApi
{
    Task<ApiCallResult<LoginResponseDto>> RegisterCompanyAsync(RegisterCompanyRequestDto request, CancellationToken cancellationToken = default);
}
