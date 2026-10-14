using TimeTracker.Domain;
using TimeTracker.Domain.TimeTracking;

namespace TimeTracker.Application.TimeTracking;

public sealed record TimeEntryResult(Guid Id, TimeEntryType Type, DateTimeOffset ClockInAt, DateTimeOffset? ClockOutAt, int? DurationMinutes)
{
    public static TimeEntryResult From(TimeEntry entry)
        => new(entry.Id, entry.Type, entry.ClockInAt, entry.ClockOutAt, entry.CalculateDuration()?.Minutes);
}

public sealed record TimeEntryListResult(
    IReadOnlyList<TimeEntryResult> Items,
    int TotalWorkedMinutes,
    int TotalBreakMinutes,
    int TotalMealBreakMinutes,
    TimeTrackingStatus Status)
{
    /// <summary>Convierte los intervalos en el listado: suma por tipo solo los cerrados (los abiertos no suman) y deduce el estado del abierto.</summary>
    public static TimeEntryListResult From(IReadOnlyList<TimeEntry> entries)
    {
        var worked = default(WorkedHours);
        var breaks = default(WorkedHours);
        var meals = default(WorkedHours);
        foreach (var entry in entries)
        {
            var duration = entry.CalculateDuration() ?? default;
            switch (entry.Type)
            {
                case TimeEntryType.Work:
                    worked += duration;
                    break;
                case TimeEntryType.Break:
                    breaks += duration;
                    break;
                default:
                    meals += duration;
                    break;
            }
        }

        var open = entries.FirstOrDefault(entry => entry.IsOpen);
        return new TimeEntryListResult([.. entries.Select(TimeEntryResult.From)], worked.Minutes, breaks.Minutes, meals.Minutes, StatusOf(open));
    }

    private static TimeTrackingStatus StatusOf(TimeEntry? open) => open?.Type switch
    {
        null => TimeTrackingStatus.Idle,
        TimeEntryType.Work => TimeTrackingStatus.Working,
        TimeEntryType.Break => TimeTrackingStatus.OnBreak,
        _ => TimeTrackingStatus.OnMealBreak,
    };
}
