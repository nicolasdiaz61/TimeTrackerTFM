namespace TimeTracker.Web.TimeEntries;

// DTO duplicado a propósito: la Web no referencia proyectos de src/ (UI-12). No lleva employeeId.
// Type es el texto de la API (Work, Break, MealBreak); se traduce al mostrarlo (TimeEntryPresentation).
public sealed record TimeEntryDto(Guid Id, string Type, DateTimeOffset ClockInAt, DateTimeOffset? ClockOutAt, int? DurationMinutes);
