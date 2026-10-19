namespace TimeTracker.Web.Assistant;

// Contrato de POST /api/asistente (docs/api/openapi.json). Los roles viajan como texto exacto «user» o «assistant».
public sealed record AssistantTurnDto(string Role, string Text);

public sealed record AssistantRequestDto(string Message, IReadOnlyList<AssistantTurnDto> History);

public sealed record AssistantResponseDto(string Reply);
