using System.Net.Http.Json;

using TimeTracker.Web.Http;

namespace TimeTracker.Web.Auth;

/// <summary>Cliente de /api/auth/login. Se registra sin BearerTokenHandler: el 401 del login no redirige.</summary>
public sealed class AuthApiClient(HttpClient http) : IDisposable
{
    public Task<LoginResponseDto> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
        => ApiRequest.SendAsync<LoginResponseDto>(
            () => http.PostAsJsonAsync("api/auth/login", new LoginRequestDto(email, password), cancellationToken),
            cancellationToken);

    public void Dispose() => http.Dispose();
}
