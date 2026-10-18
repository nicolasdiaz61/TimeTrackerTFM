namespace TimeTracker.Infrastructure.Assistant;

/// <summary>Resultados que se registran por herramienta (spec assistant-tools, registro de auditoría).</summary>
internal static class ToolOutcome
{
    public const string Ok = "ok";
    public const string Invalid = "invalid";
    public const string NotFound = "not-found";
    public const string Error = "error";
}
