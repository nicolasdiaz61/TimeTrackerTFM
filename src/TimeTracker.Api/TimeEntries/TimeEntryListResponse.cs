using TimeTracker.Application.TimeTracking;

namespace TimeTracker.Api.TimeEntries;

public sealed record TimeEntryListResponse(IReadOnlyList<TimeEntryResponse> Items, int TotalWorkedMinutes)
{
    public static TimeEntryListResponse From(TimeEntryListResult result)
        => new([.. result.Items.Select(TimeEntryResponse.From)], result.TotalWorkedMinutes);
}
