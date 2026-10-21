using TimeTracker.Application.TimeTracking;
using TimeTracker.Domain.Companies;
using TimeTracker.Domain.TimeTracking;

namespace TimeTracker.Application.Tests;

public class TimeEntryListResultTests
{
    private static readonly Guid EmployeeId = Guid.Parse("3f2b8a64-5c1d-4e7a-9b20-6d4f1a8c7e35");
    private static readonly DateTimeOffset EightAm = new(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void From_WithClosedAndOpenEntries_TotalsOnlyClosed()
    {
        var first = TimeEntry.Start(Company.DefaultId, EmployeeId, TimeEntryType.Work, EightAm);
        first.Close(EightAm.AddMinutes(60));
        var second = TimeEntry.Start(Company.DefaultId, EmployeeId, TimeEntryType.Work, EightAm.AddHours(2));
        second.Close(EightAm.AddHours(2).AddMinutes(30));
        var open = TimeEntry.Start(Company.DefaultId, EmployeeId, TimeEntryType.Work, EightAm.AddHours(4));

        var result = TimeEntryListResult.From([first, second, open], open);

        Assert.Equal(90, result.TotalWorkedMinutes);
        Assert.Equal([first.Id, second.Id, open.Id], result.Items.Select(item => item.Id));
        Assert.Null(result.Items[2].DurationMinutes);
    }

    [Fact]
    public void From_WithNoEntries_ReturnsEmptyAndZero()
    {
        var result = TimeEntryListResult.From([], null);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalWorkedMinutes);
        Assert.Equal(0, result.TotalBreakMinutes);
        Assert.Equal(0, result.TotalMealBreakMinutes);
    }

    [Fact]
    public void From_TotalsEachTypeSeparately_OnlyClosed()
    {
        var work1 = Closed(TimeEntryType.Work, EightAm, 60);
        var breakEntry = Closed(TimeEntryType.Break, EightAm.AddHours(1), 15);
        var work2 = Closed(TimeEntryType.Work, EightAm.AddHours(2), 30);
        var meal = Closed(TimeEntryType.MealBreak, EightAm.AddHours(3), 45);
        var openBreak = TimeEntry.Start(Company.DefaultId, EmployeeId, TimeEntryType.Break, EightAm.AddHours(5));

        var result = TimeEntryListResult.From([openBreak, meal, work2, breakEntry, work1], openBreak);

        Assert.Equal(90, result.TotalWorkedMinutes);
        Assert.Equal(15, result.TotalBreakMinutes);
        Assert.Equal(45, result.TotalMealBreakMinutes);
        Assert.Equal(TimeEntryType.Break, result.Items[0].Type);
        Assert.Null(result.Items[0].DurationMinutes);
    }

    [Theory]
    [InlineData(TimeEntryType.Work, TimeTrackingStatus.Working)]
    [InlineData(TimeEntryType.Break, TimeTrackingStatus.OnBreak)]
    [InlineData(TimeEntryType.MealBreak, TimeTrackingStatus.OnMealBreak)]
    public void From_StatusFollowsTheOpenEntry(TimeEntryType openType, TimeTrackingStatus expected)
    {
        var closed = Closed(TimeEntryType.Work, EightAm, 60);
        var open = TimeEntry.Start(Company.DefaultId, EmployeeId, openType, EightAm.AddHours(2));

        Assert.Equal(expected, TimeEntryListResult.From([open, closed], open).Status);
    }

    [Fact]
    public void From_WithoutOpenEntry_StatusIsIdle()
    {
        Assert.Equal(TimeTrackingStatus.Idle, TimeEntryListResult.From([Closed(TimeEntryType.Work, EightAm, 60)], null).Status);
        Assert.Equal(TimeTrackingStatus.Idle, TimeEntryListResult.From([], null).Status);
    }

    [Fact]
    public void From_StatusComesFromTheOpenEntryEvenWhenItIsNotInTheItems()
    {
        var open = TimeEntry.Start(Company.DefaultId, EmployeeId, TimeEntryType.MealBreak, EightAm);

        var result = TimeEntryListResult.From([Closed(TimeEntryType.Work, EightAm.AddDays(-40), 60)], open);

        Assert.Equal(TimeTrackingStatus.OnMealBreak, result.Status);
        Assert.Single(result.Items);
    }

    [Fact]
    public void ConcurrentChange_ExplainsTheConflictInSpanish()
    {
        var exception = new ConcurrentTimeEntryChangeException();

        Assert.Equal("El fichaje ha cambiado", exception.Title);
        Assert.False(string.IsNullOrWhiteSpace(exception.Message));
    }

    private static TimeEntry Closed(TimeEntryType type, DateTimeOffset start, int minutes)
    {
        var entry = TimeEntry.Start(Company.DefaultId, EmployeeId, type, start);
        entry.Close(start.AddMinutes(minutes));
        return entry;
    }
}
