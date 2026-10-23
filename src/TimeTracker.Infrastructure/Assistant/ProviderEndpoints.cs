namespace TimeTracker.Infrastructure.Assistant;

/// <summary>
/// URL base fijas de los proveedores que las necesitan (DD-3). Ninguna sale de la configuración ni de un Manager:
/// elegirla permitiría exfiltrar la clave o provocar SSRF. OpenAI usa el valor por defecto de su SDK; Anthropic se fija
/// porque su SDK tomaría <c>ANTHROPIC_BASE_URL</c> del entorno del proceso y podría desviar la clave.
/// </summary>
internal static class ProviderEndpoints
{
    public static readonly Uri Anthropic = new Uri("https://api.anthropic.com/");

    public static readonly Uri GeminiOpenAi = new Uri("https://generativelanguage.googleapis.com/v1beta/openai/");

    public static readonly Uri GroqOpenAi = new Uri("https://api.groq.com/openai/v1");
}
