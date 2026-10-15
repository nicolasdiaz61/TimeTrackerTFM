using TimeTracker.Web.TimeEntries;

namespace TimeTracker.Web.Tests;

public class TimeWindowQueryTests
{
    [Fact]
    public void Build_WritesUtcInstantsWithZSuffix()
    {
        var query = TimeWindowQuery.Build(
            new DateTimeOffset(2026, 9, 30, 22, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 31, 23, 0, 0, TimeSpan.Zero));

        Assert.Equal("from=2026-09-30T22%3A00%3A00Z&to=2026-10-31T23%3A00%3A00Z", query);
    }

    [Fact]
    public void Build_ConvertsInstantsWithAnOffsetToUtc()
    {
        var query = TimeWindowQuery.Build(
            new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.FromHours(2)),
            new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.FromHours(1)));

        Assert.Equal("from=2026-09-30T22%3A00%3A00Z&to=2026-10-31T23%3A00%3A00Z", query);
    }
}
