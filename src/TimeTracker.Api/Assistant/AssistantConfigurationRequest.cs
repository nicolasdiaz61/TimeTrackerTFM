using System.Text;
using System.Text.Json.Serialization;

namespace TimeTracker.Api.Assistant;

/// <summary>
/// Cuerpo del guardado: reemplazo completo (los campos omitidos vuelven al entorno) salvo <paramref name="Credential"/>, que omitida conserva la guardada.
/// Cualquier propiedad que no sea de esta lista (por ejemplo una URL) se rechaza con 400: no hay forma de dirigir la clave a otro destino (E-7).
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AssistantConfigurationRequest(
    string? Provider,
    string? Model,
    int? TimeoutSeconds,
    int? MaxOutputTokens,
    string? Effort,
    string? Credential,
    string? ConcurrencyToken)
{
    /// <summary>La clave no sale en registros ni en el texto de excepciones.</summary>
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"Provider = {Provider}, Model = {Model}, TimeoutSeconds = {TimeoutSeconds}, MaxOutputTokens = {MaxOutputTokens}, Effort = {Effort}, ConcurrencyToken = {ConcurrencyToken}, Credential = ");
        builder.Append(Credential is null ? "null" : "[clave redactada]");
        return true;
    }
}

/// <summary>Cuerpo de la prueba de conexión: los mismos campos que el guardado salvo el token; lo que falte se completa con la configuración efectiva.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AssistantConnectionTestRequest(
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
