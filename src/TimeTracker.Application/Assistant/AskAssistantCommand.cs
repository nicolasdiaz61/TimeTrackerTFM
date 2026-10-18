namespace TimeTracker.Application.Assistant;

/// <summary>Turno del historial tal cual llega del cliente: el rol puede faltar o ser inválido; el caso de uso lo comprueba.</summary>
public sealed record AssistantTurnInput(string? Role, string? Text);

/// <summary>Pregunta recibida tal cual: puede faltar o venir mal formada; el caso de uso la comprueba.</summary>
public sealed record AskAssistantCommand(string? Message, IReadOnlyList<AssistantTurnInput>? History);
