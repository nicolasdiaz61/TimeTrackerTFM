using System.Text.Json.Serialization;

using TimeTracker.Application.TimeTracking;
using TimeTracker.Domain.TimeTracking;

namespace TimeTracker.Api.TimeEntries;

// DateTime con Kind=Utc: System.Text.Json lo escribe con «Z»; un DateTimeOffset saldría con «+00:00» (DD-14).
// El tipo viaja como texto (Work, Break, MealBreak) y el contrato OpenAPI lo declara como enumeración.
public sealed record TimeEntryResponse(
    Guid Id,
    [property: JsonConverter(typeof(JsonStringEnumConverter<TimeEntryType>))] TimeEntryType Type,
    DateTime ClockInAt,
    DateTime? ClockOutAt,
    int? DurationMinutes)
{
    public static TimeEntryResponse From(TimeEntryResult result)
        => new(result.Id, result.Type, result.ClockInAt.UtcDateTime, result.ClockOutAt?.UtcDateTime, result.DurationMinutes);
}
