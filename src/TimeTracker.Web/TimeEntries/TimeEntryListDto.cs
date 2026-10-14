namespace TimeTracker.Web.TimeEntries;

// Status es el texto de la API (Idle, Working, OnBreak, OnMealBreak).
public sealed record TimeEntryListDto(
    IReadOnlyList<TimeEntryDto> Items,
    int TotalWorkedMinutes,
    int TotalBreakMinutes,
    int TotalMealBreakMinutes,
    string Status);
