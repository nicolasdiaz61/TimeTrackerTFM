using TimeTracker.Application.TimeTracking;

namespace TimeTracker.Api.TimeEntries;

// DateTime con Kind=Utc: System.Text.Json lo escribe con «Z»; un DateTimeOffset saldría con «+00:00» (DD-14).
public sealed record TimeEntryResponse(Guid Id, DateTime ClockInAt, DateTime? ClockOutAt, int? WorkedMinutes)
{
    public static TimeEntryResponse From(TimeEntryResult result)
        => new(result.Id, result.ClockInAt.UtcDateTime, result.ClockOutAt?.UtcDateTime, result.WorkedMinutes);
}
