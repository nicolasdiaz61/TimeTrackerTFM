using TimeTracker.Domain.WorkSchedules;

namespace TimeTracker.Domain.Tests.WorkSchedules;

public class WorkScheduleTests
{
    private static readonly Guid Id = Guid.Parse("0197a1b2-1c3d-7e4f-8a5b-6c7d8e9f0a1b");
    private static readonly Guid CompanyId = Guid.Parse("0197a1b2-3c3d-7e4f-8a5b-6c7d8e9f0a3d");

    private static WorkScheduleDetails Valid() => new(
        "  Jornada de oficina ",
        new TimeOnly(9, 0),
        new TimeOnly(14, 0),
        60,
        30,
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]);

    [Fact]
    public void Create_WithValidData_SetsPropertiesAndTrimsTheName()
    {
        var schedule = WorkSchedule.Create(Id, CompanyId, Valid());

        Assert.Equal(Id, schedule.Id);
        Assert.Equal(CompanyId, schedule.CompanyId);
        Assert.Equal("Jornada de oficina", schedule.Name);
        Assert.Equal(new TimeOnly(9, 0), schedule.StartTime);
        Assert.Equal(new TimeOnly(14, 0), schedule.LunchStart);
        Assert.Equal(60, schedule.LunchMinutes);
        Assert.Equal(30, schedule.ExpectedBreakMinutes);
        Assert.Equal(
            [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
            schedule.Days);
    }

    [Fact]
    public void Days_IgnoresDuplicatesAndOrder_AndRoundTripsThroughTheMask()
    {
        var details = Valid() with { Days = [DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Sunday] };

        var schedule = WorkSchedule.Create(Id, CompanyId, details);

        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Sunday], schedule.Days);
        Assert.Equal(WorkSchedule.ToMask([DayOfWeek.Monday, DayOfWeek.Sunday]), schedule.DaysMask);
    }

    [Fact]
    public void Create_WithEmptyIdentifiers_Throws()
    {
        Assert.Throws<ArgumentException>(() => WorkSchedule.Create(Guid.Empty, CompanyId, Valid()));
        Assert.Throws<ArgumentException>(() => WorkSchedule.Create(Id, Guid.Empty, Valid()));
    }

    [Fact]
    public void Validate_WithValidData_ReturnsNoErrors() => Assert.Empty(WorkSchedule.Validate(Valid()));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithBlankName_ReportsTheName(string? name)
        => Assert.Contains(WorkSchedule.Validate(Valid() with { Name = name }), error => error.Field == "name");

    [Fact]
    public void Validate_WithTooLongName_ReportsTheName()
    {
        var details = Valid() with { Name = new string('a', WorkSchedule.MaxNameLength + 1) };

        Assert.Contains(WorkSchedule.Validate(details), error => error.Field == "name");
        Assert.Empty(WorkSchedule.Validate(details with { Name = new string('a', WorkSchedule.MaxNameLength) }));
    }

    [Fact]
    public void Validate_WithMissingTimes_ReportsThem()
    {
        var errors = WorkSchedule.Validate(Valid() with { StartTime = null, LunchStart = null });

        Assert.Contains(errors, error => error.Field == "startTime");
        Assert.Contains(errors, error => error.Field == "lunchStart");
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(480, true)]
    [InlineData(481, false)]
    public void Validate_LunchMinutes_AcceptsOnlyZeroTo480(int minutes, bool valid)
        => Assert.Equal(valid, WorkSchedule.Validate(Valid() with { LunchMinutes = minutes }).All(error => error.Field != "lunchMinutes"));

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(480, true)]
    [InlineData(481, false)]
    public void Validate_ExpectedBreakMinutes_AcceptsOnlyZeroTo480(int minutes, bool valid)
        => Assert.Equal(valid, WorkSchedule.Validate(Valid() with { ExpectedBreakMinutes = minutes }).All(error => error.Field != "expectedBreakMinutes"));

    [Fact]
    public void Validate_WithMissingMinutes_ReportsThem()
    {
        var errors = WorkSchedule.Validate(Valid() with { LunchMinutes = null, ExpectedBreakMinutes = null });

        Assert.Contains(errors, error => error.Field == "lunchMinutes");
        Assert.Contains(errors, error => error.Field == "expectedBreakMinutes");
    }

    [Theory]
    [InlineData(9, 0, false)]
    [InlineData(8, 59, false)]
    [InlineData(9, 1, true)]
    public void Validate_LunchMustStartAfterTheStartTime(int hour, int minute, bool valid)
    {
        var errors = WorkSchedule.Validate(Valid() with { LunchStart = new TimeOnly(hour, minute) });

        Assert.Equal(valid, errors.All(error => error.Field != "lunchStart"));
    }

    [Fact]
    public void Validate_LunchMustEndBeforeMidnight()
    {
        var details = Valid() with { LunchStart = new TimeOnly(23, 30), LunchMinutes = 60 };

        Assert.Contains(WorkSchedule.Validate(details), error => error.Field == "lunchMinutes");
        Assert.Empty(WorkSchedule.Validate(details with { LunchMinutes = 29 }));
    }

    [Theory]
    [InlineData(null)]
    public void Validate_WithoutDays_ReportsTheDays(IReadOnlyCollection<DayOfWeek>? days)
        => Assert.Contains(WorkSchedule.Validate(Valid() with { Days = days }), error => error.Field == "days");

    [Fact]
    public void Validate_WithEmptyOrUndefinedDays_ReportsTheDays()
    {
        Assert.Contains(WorkSchedule.Validate(Valid() with { Days = [] }), error => error.Field == "days");
        Assert.Contains(WorkSchedule.Validate(Valid() with { Days = [(DayOfWeek)7] }), error => error.Field == "days");
    }

    [Fact]
    public void Create_WithInvalidData_Throws()
        => Assert.Throws<ArgumentException>(() => WorkSchedule.Create(Id, CompanyId, Valid() with { Name = "" }));

    [Fact]
    public void Update_ReplacesTheDetails_AndKeepsIdentityAndCompany()
    {
        var schedule = WorkSchedule.Create(Id, CompanyId, Valid());

        schedule.Update(new WorkScheduleDetails("Turno de tarde", new TimeOnly(15, 0), new TimeOnly(18, 0), 30, 15, [DayOfWeek.Saturday]));

        Assert.Equal(Id, schedule.Id);
        Assert.Equal(CompanyId, schedule.CompanyId);
        Assert.Equal("Turno de tarde", schedule.Name);
        Assert.Equal(new TimeOnly(15, 0), schedule.StartTime);
        Assert.Equal(new TimeOnly(18, 0), schedule.LunchStart);
        Assert.Equal(30, schedule.LunchMinutes);
        Assert.Equal(15, schedule.ExpectedBreakMinutes);
        Assert.Equal([DayOfWeek.Saturday], schedule.Days);
    }

    [Fact]
    public void Update_WithInvalidData_ThrowsAndLeavesTheScheduleUntouched()
    {
        var schedule = WorkSchedule.Create(Id, CompanyId, Valid());

        Assert.Throws<ArgumentException>(() => schedule.Update(Valid() with { Days = [] }));

        Assert.Equal("Jornada de oficina", schedule.Name);
        Assert.Equal(5, schedule.Days.Count);
    }
}
