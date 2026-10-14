using TimeTracker.Application.TimeTracking;
using TimeTracker.Domain.TimeTracking;

namespace TimeTracker.Application.Tests;

public class TimeEntryListResultTests
{
    private static readonly Guid EmployeeId = Guid.Parse("3f2b8a64-5c1d-4e7a-9b20-6d4f1a8c7e35");
    private static readonly DateTimeOffset EightAm = new(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void From_WithClosedAndOpenEntries_TotalsOnlyClosed()
    {
        var first = TimeEntry.Start(EmployeeId, EightAm);
        first.Close(EightAm.AddMinutes(60));
        var second = TimeEntry.Start(EmployeeId, EightAm.AddHours(2));
        second.Close(EightAm.AddHours(2).AddMinutes(30));
        var open = TimeEntry.Start(EmployeeId, EightAm.AddHours(4));

        var result = TimeEntryListResult.From([first, second, open]);

        Assert.Equal(90, result.TotalWorkedMinutes);
        Assert.Equal([first.Id, second.Id, open.Id], result.Items.Select(item => item.Id));
        Assert.Null(result.Items[2].WorkedMinutes);
    }

    [Fact]
    public void From_WithNoEntries_ReturnsEmptyAndZero()
    {
        var result = TimeEntryListResult.From([]);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalWorkedMinutes);
    }
}
