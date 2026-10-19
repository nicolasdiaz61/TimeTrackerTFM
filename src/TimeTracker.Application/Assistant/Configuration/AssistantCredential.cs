namespace TimeTracker.Application.Assistant.Configuration;

/// <summary>Clave del proveedor en claro. Su <c>ToString()</c> está redactado para que registros y excepciones no la filtren.</summary>
public sealed record AssistantCredential(string Value)
{
    public override string ToString() => "[clave redactada]";
}
