namespace TimeTracker.Api.Assistant;

// Solo estos campos se enlazan: employeeId, role u otros extra del cuerpo se ignoran (asignación masiva, API3).
public sealed record AssistantRequest(string? Message, IReadOnlyList<AssistantTurnRequest>? History);

/// <summary>Turno del historial; el rol se valida como texto exacto en Application, no aquí.</summary>
public sealed record AssistantTurnRequest(string? Role, string? Text);
