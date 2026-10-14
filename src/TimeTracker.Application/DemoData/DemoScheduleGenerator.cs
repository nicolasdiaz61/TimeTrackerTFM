using TimeTracker.Domain.TimeTracking;

namespace TimeTracker.Application.DemoData;

/// <summary>
/// Genera la agenda de un día de demostración: función pura de (empleado, perfil, fecha), sin reloj ni aleatoriedad
/// no controlada. Construye los intervalos con las operaciones del dominio, que valida cada transición.
/// </summary>
public static class DemoScheduleGenerator
{
    private const int MinutesPerHour = 60;
    private const int DaysPerWeek = 7;
    private const int WorkdaysPerWeek = 5;
    private const int WorkdaysBetweenAbsences = 12;
    private const int MinWorkMinutes = 15;
    private const int PercentScale = 100;

    /// <summary>Lunes a viernes que no son día de ausencia del perfil.</summary>
    public static bool IsWorkday(DemoScheduleProfile profile, DateOnly date)
    {
        // DayNumber 0 es el lunes 1/1/1 del calendario gregoriano: el resto de dividir entre 7 es el día de la semana (0 = lunes).
        var weekday = date.DayNumber % DaysPerWeek;
        if (weekday >= WorkdaysPerWeek)
        {
            return false;
        }

        var workdayIndex = (date.DayNumber / DaysPerWeek * WorkdaysPerWeek) + weekday;
        return (workdayIndex + profile.AbsenceOffset) % WorkdaysBetweenAbsences != 0;
    }

    /// <summary>Intervalos del día, cerrados y consecutivos; vacío si no es laborable.</summary>
    public static IReadOnlyList<TimeEntry> GenerateDay(Guid employeeId, DemoScheduleProfile profile, DateOnly date)
    {
        if (!IsWorkday(profile, date))
        {
            return [];
        }

        var plan = profile.PlanFor(date.DayOfWeek);
        var random = new DemoRandom(profile.Seed, date.DayNumber);
        var start = Minutes(plan.Start) + random.Next(-profile.Variation, profile.Variation + 1);
        var end = Minutes(plan.End) + random.Next(-profile.Variation, profile.Variation + 1);

        var current = TimeEntry.Start(employeeId, TimeEntryType.Work, Instant(date, start));
        var entries = new List<TimeEntry> { current };
        var cursor = start;
        foreach (var planned in plan.Breaks)
        {
            // Se consumen siempre los mismos números por pausa: omitir una no altera la variación de las demás.
            var included = random.Next(0, PercentScale) < planned.Probability;
            var breakStart = Minutes(planned.At) + random.Next(-planned.Variation, planned.Variation + 1);
            var breakEnd = breakStart + planned.Minutes + random.Next(-planned.Variation, planned.Variation + 1);
            if (!included || breakStart < cursor + MinWorkMinutes || breakEnd > end - MinWorkMinutes)
            {
                continue;
            }

            var pause = current.Pause(planned.Type, Instant(date, breakStart));
            current = pause.Resume(Instant(date, breakEnd));
            entries.Add(pause);
            entries.Add(current);
            cursor = breakEnd;
        }

        current.Close(Instant(date, end));
        return entries;
    }

    private static int Minutes(TimeOnly time) => (time.Hour * MinutesPerHour) + time.Minute;

    private static DateTimeOffset Instant(DateOnly date, int minutesSinceMidnight)
        => SpainTime.ToUtc(date, TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(minutesSinceMidnight)));
}
