using System.Text;

namespace TimeTracker.Application.Assistant.Configuration;

/// <summary>Entrada en texto tal como llega de la API, para validar aquí y devolver mensajes en español.</summary>
public sealed record AssistantSettingsInput(
    string? Provider,
    string? Model,
    int? TimeoutSeconds,
    int? MaxOutputTokens,
    string? Effort,
    string? Credential)
{
    /// <summary>La clave viaja en <c>string</c>: se omite del texto del registro para que registros y excepciones no la filtren.</summary>
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"Provider = {Provider}, Model = {Model}, TimeoutSeconds = {TimeoutSeconds}, MaxOutputTokens = {MaxOutputTokens}, Effort = {Effort}, Credential = ");
        builder.Append(Credential is null ? "null" : "[clave redactada]");
        return true;
    }
}
