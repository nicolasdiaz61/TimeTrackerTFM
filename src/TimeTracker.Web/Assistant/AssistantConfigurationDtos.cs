using System.Text;

namespace TimeTracker.Web.Assistant;

// Contrato de /api/asistente/configuracion (docs/api/openapi.json). La API no tiene ningún campo de URL ni devuelve la clave.
public sealed record AllowedProviderDto(string Provider, bool RequiresCredential, bool SupportsEffort);

/// <summary>Origen de cada valor efectivo: <c>env</c> o <c>stored</c> (la clave también puede ser <c>none</c>).</summary>
public sealed record AssistantConfigurationSourcesDto(string Provider, string Model, string TimeoutSeconds, string MaxOutputTokens, string Effort, string Credential);

public sealed record AssistantConfigurationDto(
    string Provider,
    string? Model,
    int TimeoutSeconds,
    int MaxOutputTokens,
    string? Effort,
    AssistantConfigurationSourcesDto Sources,
    bool CredentialConfigured,
    bool CredentialStorageAvailable,
    IReadOnlyList<AllowedProviderDto> AllowedProviders,
    string ConcurrencyToken,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    bool Applied);

/// <summary>Un campo <see langword="null"/> significa «usar el valor del entorno».</summary>
public sealed record AssistantConfigurationRequestDto(
    string? Provider,
    string? Model,
    int? TimeoutSeconds,
    int? MaxOutputTokens,
    string? Effort,
    string? Credential,
    string ConcurrencyToken)
{
    // La clave no debe llegar a un registro ni a un mensaje de excepción por un ToString accidental.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"Provider = {Provider}, Model = {Model}, TimeoutSeconds = {TimeoutSeconds}, MaxOutputTokens = {MaxOutputTokens}, Effort = {Effort}, Credential = ");
        builder.Append(Credential is null ? "null" : "[clave redactada]");
        builder.Append($", ConcurrencyToken = {ConcurrencyToken}");
        return true;
    }
}

public sealed record AssistantConnectionTestRequestDto(
    string? Provider,
    string? Model,
    int? TimeoutSeconds,
    int? MaxOutputTokens,
    string? Effort,
    string? Credential)
{
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"Provider = {Provider}, Model = {Model}, TimeoutSeconds = {TimeoutSeconds}, MaxOutputTokens = {MaxOutputTokens}, Effort = {Effort}, Credential = ");
        builder.Append(Credential is null ? "null" : "[clave redactada]");
        return true;
    }
}

public sealed record AssistantConnectionTestResponseDto(string Result, string Message);
