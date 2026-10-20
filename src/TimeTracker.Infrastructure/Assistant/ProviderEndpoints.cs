namespace TimeTracker.Infrastructure.Assistant;

/// <summary>
/// URL base fijas de los proveedores que las necesitan (DD-3). Ninguna sale de la configuración ni de un Manager:
/// elegirla permitiría exfiltrar la clave o provocar SSRF. Anthropic y OpenAI usan el valor por defecto de su SDK.
/// </summary>
internal static class ProviderEndpoints
{
    public static readonly Uri GeminiOpenAi = new("https://generativelanguage.googleapis.com/v1beta/openai/");
}
