namespace TimeTracker.Web.TimeEntries;

// DTO duplicado a propósito: la Web no referencia proyectos de src/ (UI-12). No lleva employeeId.
public sealed record TimeEntryDto(Guid Id, DateTimeOffset ClockInAt, DateTimeOffset? ClockOutAt, int? WorkedMinutes);
