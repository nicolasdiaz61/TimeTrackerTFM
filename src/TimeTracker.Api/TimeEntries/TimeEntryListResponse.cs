using System.Text.Json.Serialization;

using TimeTracker.Application.TimeTracking;

namespace TimeTracker.Api.TimeEntries;

public sealed record TimeEntryListResponse(
    IReadOnlyList<TimeEntryResponse> Items,
    int TotalWorkedMinutes,
    int TotalBreakMinutes,
    int TotalMealBreakMinutes,
    [property: JsonConverter(typeof(JsonStringEnumConverter<TimeTrackingStatus>))] TimeTrackingStatus Status)
{
    public static TimeEntryListResponse From(TimeEntryListResult result)
        => new(
            [.. result.Items.Select(TimeEntryResponse.From)],
            result.TotalWorkedMinutes,
            result.TotalBreakMinutes,
            result.TotalMealBreakMinutes,
            result.Status);
}
