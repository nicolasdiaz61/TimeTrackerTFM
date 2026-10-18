namespace TimeTracker.Application.Assistant;

/// <summary>Puerto del motor del asistente (modelo y herramientas); lo implementa Infrastructure.</summary>
public interface IAssistantEngine
{
    bool IsEnabled { get; }

    /// <summary>Respuesta del asistente, o <c>null</c> si el proveedor no está disponible.</summary>
    Task<string?> ReplyAsync(AssistantConversation conversation, CancellationToken cancellationToken);
}
