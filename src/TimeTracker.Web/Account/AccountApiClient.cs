using System.Net.Http.Json;

using TimeTracker.Web.Auth;
using TimeTracker.Web.Http;

namespace TimeTracker.Web.Account;

/// <summary>Cliente tipado de /api/auth/change-password. Lleva el BearerTokenHandler: el 401 cierra la sesión y lleva a /login.</summary>
public sealed class AccountApiClient(HttpClient http) : IPasswordChangeApi, IDisposable
{
    public Task<ApiCallResult<LoginResponseDto>> ChangePasswordAsync(ChangePasswordRequestDto request, CancellationToken cancellationToken = default)
        => ApiCall.SendAsync<LoginResponseDto>(() => http.PostAsJsonAsync("api/auth/change-password", request, cancellationToken), cancellationToken);

    public void Dispose() => http.Dispose();
}
