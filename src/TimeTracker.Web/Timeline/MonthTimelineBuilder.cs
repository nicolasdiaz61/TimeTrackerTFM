using TimeTracker.Web.Calendar;
using TimeTracker.Web.Time;
using TimeTracker.Web.TimeEntries;

namespace TimeTracker.Web.Timeline;

/// <summary>Construye la vista de un mes en la hora local del usuario a partir de los intervalos de la API.</summary>
public static class MonthTimelineBuilder
{
    private const int SecondsPerHour = 3600;

    public static MonthTimeline Build(MonthRef month, IReadOnlyList<TimeEntryDto> items, ILocalTimeZone zone, DateTimeOffset now)
    {
        var today = zone.DateOf(now);
        var current = MonthRef.Of(today);

        // Mes actual: hasta hoy (los días futuros no tienen sentido); mes pasado: completo; mes futuro: ninguno.
        var lastDay = month.IsCurrent(current) ? today : month.CanGoNext(current) ? month.LastDay : month.FirstDay.AddDays(-1);
        var days = new List<DayTimeline>();
        for (var date = lastDay; date >= month.FirstDay; date = date.AddDays(-1))
        {
            days.Add(DayTimelineBuilder.Build(date, items, zone, now));
        }

        return new MonthTimeline(
            month,
            days,
            AxisFor(days),
            days.Sum(day => day.WorkedMinutes),
            days.Sum(day => day.BreakMinutes),
            days.Sum(day => day.MealMinutes),
            days.Count(day => day.HasEntries));
    }

    // Mínimo 06:00-22:00, ampliado a horas enteras si algún tramo del mes queda fuera; el mismo para todas las filas.
    private static TimelineAxis AxisFor(List<DayTimeline> days)
    {
        var segments = days.SelectMany(day => day.Segments).ToList();
        if (segments.Count == 0)
        {
            return TimelineAxis.Default;
        }

        var startHour = Math.Min(TimelineAxis.Default.StartHour, segments.Min(segment => segment.StartSecond) / SecondsPerHour);
        var endHour = Math.Max(TimelineAxis.Default.EndHour, (segments.Max(segment => segment.EndSecond) + SecondsPerHour - 1) / SecondsPerHour);
        return new TimelineAxis(startHour, endHour);
    }
}
