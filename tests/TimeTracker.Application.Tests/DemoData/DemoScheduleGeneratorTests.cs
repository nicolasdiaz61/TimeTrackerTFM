using TimeTracker.Application.DemoData;
using TimeTracker.Application.Time;
using TimeTracker.Domain.Companies;
using TimeTracker.Domain.TimeTracking;

namespace TimeTracker.Application.Tests.DemoData;

public class DemoScheduleGeneratorTests
{
    private static readonly Guid Employee = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly DateOnly WindowStart = new(2026, 9, 14);

    private static readonly DemoScheduleProfile[] AllProfiles =
    [
        DemoScheduleProfiles.Manager,
        DemoScheduleProfiles.EarlyBird,
        DemoScheduleProfiles.LateShift,
        DemoScheduleProfiles.HalfDay,
    ];

    public static TheoryData<int> ProfileIndexes => [0, 1, 2, 3];

    [Theory]
    [MemberData(nameof(ProfileIndexes))]
    public void GenerateDay_SameInputs_ProducesSameTypesAndInstants(int index)
    {
        var profile = AllProfiles[index];

        foreach (var date in Window(WindowStart, 30))
        {
            var first = Signature(DemoScheduleGenerator.GenerateDay(Company.DefaultId, Employee, profile, date));
            var second = Signature(DemoScheduleGenerator.GenerateDay(Company.DefaultId, Employee, profile, date));

            Assert.Equal(first, second);
        }
    }

    [Theory]
    [MemberData(nameof(ProfileIndexes))]
    public void GenerateDay_IsIndependentOfTheWindow(int index)
    {
        // Un día generado en una ventana y en otra desplazada es el mismo: la ventana móvil no cambia los días ya sembrados.
        var profile = AllProfiles[index];
        var date = new DateOnly(2026, 10, 7);

        var viaFirstWindow = Window(WindowStart, 30).Where(day => day == date).Select(day => Signature(DemoScheduleGenerator.GenerateDay(Company.DefaultId, Employee, profile, day)));
        var viaSecondWindow = Window(WindowStart.AddDays(3), 30).Where(day => day == date).Select(day => Signature(DemoScheduleGenerator.GenerateDay(Company.DefaultId, Employee, profile, day)));

        Assert.Equal(viaFirstWindow, viaSecondWindow);
    }

    [Theory]
    [MemberData(nameof(ProfileIndexes))]
    public void GenerateDay_CreatesEveryIntervalInTheGivenCompany(int index)
    {
        var otherCompany = Guid.Parse("0197a1b2-9c3d-7e4f-8a5b-6c7d8e9f0a9d");
        var entries = Window(WindowStart, 14)
            .SelectMany(day => DemoScheduleGenerator.GenerateDay(otherCompany, Employee, AllProfiles[index], day))
            .ToList();

        Assert.NotEmpty(entries);
        Assert.All(entries, entry => Assert.Equal(otherCompany, entry.CompanyId));
    }

    [Theory]
    [MemberData(nameof(ProfileIndexes))]
    public void GenerateDay_ProducesClosedConsecutiveIntervalsFollowingTheDomainRules(int index)
    {
        var profile = AllProfiles[index];

        foreach (var date in Window(WindowStart, 90))
        {
            var entries = DemoScheduleGenerator.GenerateDay(Company.DefaultId, Employee, profile, date);

            if (entries.Count == 0)
            {
                continue;
            }

            Assert.All(entries, entry =>
            {
                Assert.Equal(Employee, entry.EmployeeId);
                Assert.False(entry.IsOpen);
                Assert.True(entry.ClockOutAt > entry.ClockInAt);
            });
            Assert.Equal(TimeEntryType.Work, entries[0].Type);
            Assert.Equal(TimeEntryType.Work, entries[^1].Type);
            for (var position = 1; position < entries.Count; position++)
            {
                Assert.Equal(entries[position - 1].ClockOutAt, entries[position].ClockInAt);
                Assert.Equal(position % 2 == 0, entries[position].Type == TimeEntryType.Work);
            }
        }
    }

    [Theory]
    [MemberData(nameof(ProfileIndexes))]
    public void GenerateDay_OnWeekends_ProducesNothing(int index)
    {
        var profile = AllProfiles[index];

        var weekend = Window(WindowStart, 90).Where(day => day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday).ToList();

        Assert.NotEmpty(weekend);
        Assert.All(weekend, day => Assert.Empty(DemoScheduleGenerator.GenerateDay(Company.DefaultId, Employee, profile, day)));
    }

    [Theory]
    [MemberData(nameof(ProfileIndexes))]
    public void AnyThirtyDayWindow_HasOneOrTwoAbsentWeekdays(int index)
    {
        var profile = AllProfiles[index];

        for (var offset = 0; offset < 60; offset++)
        {
            var absent = Window(WindowStart.AddDays(offset), 30)
                .Count(day => day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)
                    && DemoScheduleGenerator.GenerateDay(Company.DefaultId, Employee, profile, day).Count == 0);

            Assert.InRange(absent, 1, 2);
        }
    }

    [Fact]
    public void Profiles_DifferFromEachOther()
    {
        var summaries = AllProfiles
            .Select(profile => Window(WindowStart, 30)
                .SelectMany(day => DemoScheduleGenerator.GenerateDay(Company.DefaultId, Employee, profile, day))
                .Aggregate((Work: 0, Breaks: 0, Meals: 0), (total, entry) => entry.Type switch
                {
                    TimeEntryType.Work => (total.Work + entry.CalculateDuration()!.Value.Minutes, total.Breaks, total.Meals),
                    TimeEntryType.Break => (total.Work, total.Breaks + 1, total.Meals),
                    _ => (total.Work, total.Breaks, total.Meals + 1),
                }))
            .ToList();

        Assert.Equal(4, summaries.Distinct().Count());
    }

    [Fact]
    public void LateShift_OnWednesday_WorksFromHomeWithoutMealBreak()
    {
        var wednesdays = Window(WindowStart, 90).Where(day => day.DayOfWeek == DayOfWeek.Wednesday).ToList();

        var entries = wednesdays.SelectMany(day => DemoScheduleGenerator.GenerateDay(Company.DefaultId, Employee, DemoScheduleProfiles.LateShift, day)).ToList();

        Assert.NotEmpty(entries);
        Assert.DoesNotContain(entries, entry => entry.Type == TimeEntryType.MealBreak);
        Assert.Contains(entries, entry => entry.Type == TimeEntryType.Break);
    }

    [Fact]
    public void LateShift_OtherWeekdays_HaveLongMealBreaks()
    {
        var mondays = Window(WindowStart, 90).Where(day => day.DayOfWeek == DayOfWeek.Monday);

        var meals = mondays
            .SelectMany(day => DemoScheduleGenerator.GenerateDay(Company.DefaultId, Employee, DemoScheduleProfiles.LateShift, day))
            .Where(entry => entry.Type == TimeEntryType.MealBreak)
            .ToList();

        Assert.NotEmpty(meals);
        Assert.All(meals, meal => Assert.InRange(meal.CalculateDuration()!.Value.Minutes, 65, 85));
    }

    [Fact]
    public void HalfDay_HasNoMealBreaksAndAtMostOneBreakPerDay()
    {
        foreach (var date in Window(WindowStart, 30))
        {
            var entries = DemoScheduleGenerator.GenerateDay(Company.DefaultId, Employee, DemoScheduleProfiles.HalfDay, date);

            Assert.DoesNotContain(entries, entry => entry.Type == TimeEntryType.MealBreak);
            Assert.True(entries.Count(entry => entry.Type == TimeEntryType.Break) <= 1);
        }
    }

    [Fact]
    public void EarlyBird_HasAnHourLongMealBreakWithMinuteVariation()
    {
        var meals = Window(WindowStart, 30)
            .SelectMany(day => DemoScheduleGenerator.GenerateDay(Company.DefaultId, Employee, DemoScheduleProfiles.EarlyBird, day))
            .Where(entry => entry.Type == TimeEntryType.MealBreak)
            .Select(entry => entry.CalculateDuration()!.Value.Minutes)
            .ToList();

        Assert.NotEmpty(meals);
        Assert.All(meals, minutes => Assert.InRange(minutes, 54, 66));
        Assert.True(meals.Distinct().Count() > 1, "La duración de la comida debe variar de un día a otro.");
    }

    [Fact]
    public void Manager_HasVariableBreaksAcrossDays()
    {
        var breaksPerDay = Window(WindowStart, 60)
            .Select(day => DemoScheduleGenerator.GenerateDay(Company.DefaultId, Employee, DemoScheduleProfiles.Manager, day))
            .Where(entries => entries.Count > 0)
            .Select(entries => entries.Count(entry => entry.Type != TimeEntryType.Work))
            .Distinct()
            .ToList();

        Assert.True(breaksPerDay.Count > 1, "El número de pausas del día debe variar.");
    }

    [Fact]
    public void GenerateDay_SkipsBreaksThatDoNotFit_AndKeepsAValidSchedule()
    {
        var profile = Fixed(new DemoDayPlan(
            new TimeOnly(8, 0),
            new TimeOnly(10, 0),
            [
                Plan(new TimeOnly(8, 5), minutes: 5, probability: 100), // demasiado cerca de la entrada
                Plan(new TimeOnly(9, 0), minutes: 15, probability: 100), // cabe
                Plan(new TimeOnly(9, 10), minutes: 5, probability: 100), // se solapa con la anterior
                Plan(new TimeOnly(9, 50), minutes: 10, probability: 100), // acaba demasiado cerca de la salida
                Plan(new TimeOnly(9, 30), minutes: 5, probability: 0), // no ocurre ese día
            ]));
        var date = FirstWorkday(profile, WindowStart);

        var entries = DemoScheduleGenerator.GenerateDay(Company.DefaultId, Employee, profile, date);

        Assert.Equal(
            [
                (TimeEntryType.Work, SpainTime.ToUtc(date, new TimeOnly(8, 0)), SpainTime.ToUtc(date, new TimeOnly(9, 0))),
                (TimeEntryType.Break, SpainTime.ToUtc(date, new TimeOnly(9, 0)), SpainTime.ToUtc(date, new TimeOnly(9, 15))),
                (TimeEntryType.Work, SpainTime.ToUtc(date, new TimeOnly(9, 15)), SpainTime.ToUtc(date, new TimeOnly(10, 0))),
            ],
            entries.Select(entry => (entry.Type, entry.ClockInAt, entry.ClockOutAt!.Value)));
    }

    [Fact]
    public void GenerateDay_SameLocalSchedule_ShiftsOneHourInUtcBetweenSummerAndWinter()
    {
        var profile = Fixed(new DemoDayPlan(new TimeOnly(8, 0), new TimeOnly(16, 0), []));
        var summer = FirstWorkday(profile, new DateOnly(2026, 7, 13));
        var winter = FirstWorkday(profile, new DateOnly(2026, 1, 12));

        var inSummer = DemoScheduleGenerator.GenerateDay(Company.DefaultId, Employee, profile, summer).Single();
        var inWinter = DemoScheduleGenerator.GenerateDay(Company.DefaultId, Employee, profile, winter).Single();

        Assert.Equal(TimeSpan.FromHours(6), inSummer.ClockInAt.TimeOfDay);
        Assert.Equal(TimeSpan.FromHours(14), inSummer.ClockOutAt!.Value.TimeOfDay);
        Assert.Equal(TimeSpan.FromHours(7), inWinter.ClockInAt.TimeOfDay);
        Assert.Equal(TimeSpan.FromHours(15), inWinter.ClockOutAt!.Value.TimeOfDay);
    }

    [Fact]
    public void GenerateDay_AcrossTheOctoberChange_KeepsTheLocalSchedule()
    {
        var profile = Fixed(new DemoDayPlan(new TimeOnly(8, 0), new TimeOnly(16, 0), []));
        var friday = FirstWorkday(profile, new DateOnly(2026, 10, 23));
        var monday = FirstWorkday(profile, new DateOnly(2026, 10, 26));

        var beforeChange = DemoScheduleGenerator.GenerateDay(Company.DefaultId, Employee, profile, friday).Single();
        var afterChange = DemoScheduleGenerator.GenerateDay(Company.DefaultId, Employee, profile, monday).Single();

        Assert.Equal(TimeSpan.FromHours(6), beforeChange.ClockInAt.TimeOfDay);
        Assert.Equal(TimeSpan.FromHours(7), afterChange.ClockInAt.TimeOfDay);
        Assert.Equal(480, beforeChange.CalculateDuration()!.Value.Minutes);
        Assert.Equal(480, afterChange.CalculateDuration()!.Value.Minutes);
    }

    [Fact]
    public void ProfileOverrides_ApplyOnlyToTheirWeekday()
    {
        var standard = new DemoDayPlan(new TimeOnly(8, 0), new TimeOnly(16, 0), []);
        var friday = new DemoDayPlan(new TimeOnly(8, 0), new TimeOnly(14, 0), []);
        var profile = Fixed(standard) with { Overrides = new Dictionary<DayOfWeek, DemoDayPlan> { [DayOfWeek.Friday] = friday } };

        Assert.Same(friday, profile.PlanFor(DayOfWeek.Friday));
        Assert.Same(standard, profile.PlanFor(DayOfWeek.Monday));
        Assert.Same(standard, Fixed(standard).PlanFor(DayOfWeek.Friday));
    }

    // Sin variación, para fijar las horas exactas.
    private static DemoScheduleProfile Fixed(DemoDayPlan plan) => new(Seed: 7, AbsenceOffset: 1, Variation: 0, plan);

    private static DemoBreakPlan Plan(TimeOnly at, int minutes, int probability)
        => new(TimeEntryType.Break, at, minutes, Variation: 0, probability);

    private static DateOnly FirstWorkday(DemoScheduleProfile profile, DateOnly? from = null)
        => Window(from ?? new DateOnly(2026, 10, 12), 14).First(day => DemoScheduleGenerator.IsWorkday(profile, day));

    private static IEnumerable<DateOnly> Window(DateOnly start, int days) => Enumerable.Range(0, days).Select(start.AddDays);

    private static List<(TimeEntryType, DateTimeOffset, DateTimeOffset?)> Signature(IEnumerable<TimeEntry> entries)
        => [.. entries.Select(entry => (entry.Type, entry.ClockInAt, entry.ClockOutAt))];
}
