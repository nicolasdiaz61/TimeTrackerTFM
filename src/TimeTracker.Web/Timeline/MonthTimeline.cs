using TimeTracker.Web.Calendar;

namespace TimeTracker.Web.Timeline;

/// <summary>Vista de un mes: días (del más reciente al más antiguo), eje común y totales de los tramos recortados por día.</summary>
public sealed record MonthTimeline(
    MonthRef Month,
    IReadOnlyList<DayTimeline> Days,
    TimelineAxis Axis,
    int WorkedMinutes,
    int BreakMinutes,
    int MealMinutes,
    int DaysWithEntries)
{
    public bool IsEmpty => DaysWithEntries == 0;
}
