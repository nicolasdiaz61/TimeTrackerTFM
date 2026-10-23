namespace TimeTracker.Application.Assistant.Configuration;

/// <summary>Catálogo cerrado; los nombres coinciden con <c>AiProvider</c> de Infrastructure (una prueba lo fija).</summary>
public enum AssistantProvider
{
    None,
    Anthropic,
    OpenAI,
    Gemini,
    Ollama,
    Groq,
}
