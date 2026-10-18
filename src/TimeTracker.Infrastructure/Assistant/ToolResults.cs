namespace TimeTracker.Infrastructure.Assistant;

/// <summary>Resultado de error de una herramienta: el modelo lo recibe como dato y puede corregirse (DD-8).</summary>
internal sealed record ToolError(string Error);

internal static class ToolResults
{
    // Una única cadena para miembro ajeno, inexistente, propio o identificador ilegible (DD-9, equivalente al 404 idéntico).
    public const string MemberNotFoundMessage = "No hay ningún miembro de tu equipo con ese identificador.";

    public static ToolError Error(string message) => new(message);

    public static ToolError MemberNotFound() => new(MemberNotFoundMessage);
}
