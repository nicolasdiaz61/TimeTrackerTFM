using TimeTracker.Domain;
using TimeTracker.Domain.TimeTracking;

namespace TimeTracker.Application.TimeTracking;

public sealed record TimeEntryResult(Guid Id, DateTimeOffset ClockInAt, DateTimeOffset? ClockOutAt, int? WorkedMinutes)
{
    public static TimeEntryResult From(TimeEntry entry)
        => new(entry.Id, entry.ClockInAt, entry.ClockOutAt, entry.CalculateWorked()?.Minutes);
}

public sealed record TimeEntryListResult(IReadOnlyList<TimeEntryResult> Items, int TotalWorkedMinutes)
{
    /// <summary>Convierte los fichajes en el listado y suma solo los cerrados (los abiertos no suman).</summary>
    public static TimeEntryListResult From(IReadOnlyList<TimeEntry> entries)
    {
        var total = default(WorkedHours);
        foreach (var entry in entries)
        {
            total += entry.CalculateWorked() ?? default;
        }

        return new TimeEntryListResult([.. entries.Select(TimeEntryResult.From)], total.Minutes);
    }
}
