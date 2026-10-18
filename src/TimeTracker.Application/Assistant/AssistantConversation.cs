namespace TimeTracker.Application.Assistant;

/// <summary>Únicos roles que admite el historial del cliente: nunca <c>system</c> ni <c>tool</c>.</summary>
public enum ConversationRole
{
    User = 0,
    Assistant = 1,
}

public sealed record ConversationTurn(ConversationRole Role, string Text);

/// <summary>Conversación ya validada que recibe el motor: historial no confiable y mensaje recortado.</summary>
public sealed record AssistantConversation(IReadOnlyList<ConversationTurn> History, string Message);
