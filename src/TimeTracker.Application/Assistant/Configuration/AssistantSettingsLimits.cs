namespace TimeTracker.Application.Assistant.Configuration;

/// <summary>Fuente única de los límites; el validador de opciones de Infrastructure los reutiliza.</summary>
public static class AssistantSettingsLimits
{
    public const int MinTimeoutSeconds = 5;
    public const int MaxTimeoutSeconds = 55;
    public const int MinOutputTokens = 64;
    public const int MaxOutputTokens = 4096;
    public const int MaxModelLength = 100;
}
