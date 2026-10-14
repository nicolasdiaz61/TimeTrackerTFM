namespace TimeTracker.Web.TimeEntries;

public sealed record TimeEntryListDto(IReadOnlyList<TimeEntryDto> Items, int TotalWorkedMinutes);
