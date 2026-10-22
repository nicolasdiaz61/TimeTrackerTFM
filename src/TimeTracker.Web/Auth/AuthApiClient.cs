using System.Net.Http.Json;

using TimeTracker.Web.Http;
using TimeTracker.Web.Registration;

namespace TimeTracker.Web.Auth;

/// <summary>
/// Cliente de las rutas anónimas /api/auth/login y /api/auth/register-company. Se registra sin BearerTokenHandler: el 401 del
/// login no redirige y el registro nunca lleva «Authorization» aunque haya sesión.
/// </summary>
public sealed class AuthApiClient(HttpClient http) : IRegistrationApi, IDisposable
{
    public Task<LoginResponseDto> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
        => ApiRequest.SendAsync<LoginResponseDto>(
            () => http.PostAsJsonAsync("api/auth/login", new LoginRequestDto(email, password), cancellationToken),
            cancellationToken);

    public Task<ApiCallResult<LoginResponseDto>> RegisterCompanyAsync(RegisterCompanyRequestDto request, CancellationToken cancellationToken = default)
        => ApiCall.SendAsync<LoginResponseDto>(() => http.PostAsJsonAsync("api/auth/register-company", request, cancellationToken), cancellationToken);

    public void Dispose() => http.Dispose();
}
