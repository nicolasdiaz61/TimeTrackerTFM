using TimeTracker.Web.WorkSchedules;

namespace TimeTracker.Web.Tests.WorkSchedules;

public class WorkSchedulePlannedTests
{
    private static readonly WorkScheduleDto Office = new(
        Guid.NewGuid(),
        "Oficina",
        new TimeOnly(9, 0),
        new TimeOnly(14, 0),
        60,
        15,
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
        3);

    [Fact]
    public void PlannedFor_AWorkingDayDescribesEntryLunchAndBreak()
        => Assert.Equal("09:00 · almuerzo 14:00 (60 min) · pausa 15 min", WorkSchedulePresentation.PlannedFor(Office, new DateOnly(2026, 10, 19)));

    [Fact]
    public void PlannedFor_ADayOutsideTheScheduleIsNotWorkable()
        => Assert.Equal("No laborable", WorkSchedulePresentation.PlannedFor(Office, new DateOnly(2026, 10, 24)));

    [Fact]
    public void Overview_SummarisesNameEntryLunchBreakAndDays()
        => Assert.Equal(
            "Oficina: entrada 09:00, almuerzo 14:00 (60 min), pausa esperada 15 min, lunes, martes, miércoles, jueves, viernes",
            WorkSchedulePresentation.Summary(Office));

    [Fact]
    public void Resolve_FindsTheMemberScheduleInTheCompanyList()
    {
        var other = Office with { Id = Guid.NewGuid(), Name = "Tarde" };

        Assert.Same(other, WorkSchedulePresentation.Resolve([Office, other], other.Id));
    }

    [Fact]
    public void Resolve_WithoutAssignmentOrUnknownIdIsNull()
    {
        Assert.Null(WorkSchedulePresentation.Resolve([Office], null));
        Assert.Null(WorkSchedulePresentation.Resolve([Office], Guid.NewGuid()));
    }
}
