using System.Net.Http.Json;

using TimeTracker.Web.Http;

namespace TimeTracker.Web.Team;

// Contrato de /api/employees (docs/api/openapi.json). La Web nunca envía un identificador de compañía: la compañía sale del token.
// ManagerId es opcional (null = el propio Manager que da de alta).
public sealed record CreateEmployeeRequestDto(string DisplayName, string Email, string Role, Guid? ManagerId);

public sealed record CreateEmployeeResponseDto(Guid Id, string DisplayName, string Email, string Role, string TemporaryPassword);

public sealed record TemporaryPasswordResponseDto(string TemporaryPassword);

public interface IEmployeesApi
{
    Task<ApiCallResult<CreateEmployeeResponseDto>> CreateAsync(CreateEmployeeRequestDto request, CancellationToken cancellationToken = default);

    Task<ApiCallResult<TemporaryPasswordResponseDto>> RegenerateTemporaryPasswordAsync(Guid memberId, CancellationToken cancellationToken = default);
}

/// <summary>Cliente tipado de /api/employees (solo Manager): alta y regeneración de la contraseña temporal. El 401 lo trata el <c>BearerTokenHandler</c>.</summary>
public sealed class EmployeesApiClient(HttpClient http) : IEmployeesApi, IDisposable
{
    public Task<ApiCallResult<CreateEmployeeResponseDto>> CreateAsync(CreateEmployeeRequestDto request, CancellationToken cancellationToken = default)
        => ApiCall.SendAsync<CreateEmployeeResponseDto>(() => http.PostAsJsonAsync("api/employees", request, cancellationToken), cancellationToken);

    public Task<ApiCallResult<TemporaryPasswordResponseDto>> RegenerateTemporaryPasswordAsync(Guid memberId, CancellationToken cancellationToken = default)
        => ApiCall.SendAsync<TemporaryPasswordResponseDto>(
            () => http.PostAsync(new Uri($"api/employees/{memberId:D}/temporary-password", UriKind.Relative), content: null, cancellationToken),
            cancellationToken);

    public void Dispose() => http.Dispose();
}
