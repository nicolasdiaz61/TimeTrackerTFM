using TimeTracker.Web.Calendar;
using TimeTracker.Web.Time;
using TimeTracker.Web.TimeEntries;

namespace TimeTracker.Web.Timeline;

/// <summary>Recorta los intervalos de la API al día local y los convierte en tramos.</summary>
public static class DayTimelineBuilder
{
    private const int SecondsPerDay = 86400;

    public static DayTimeline Build(DateOnly date, IEnumerable<TimeEntryDto> items, ILocalTimeZone zone, DateTimeOffset now)
    {
        var dayStart = zone.StartOfDay(date);
        var dayEnd = zone.StartOfDay(date.AddDays(1));

        var segments = new List<TimelineSegment>();
        foreach (var item in items)
        {
            if (!SegmentKinds.TryParse(item.Type, out var kind))
            {
                continue;
            }

            // Un intervalo abierto se dibuja hasta «ahora»; si empieza en el futuro (reloj desajustado) no se dibuja.
            var start = item.ClockInAt > dayStart ? item.ClockInAt : dayStart;
            var end = item.ClockOutAt ?? now;
            var stop = end < dayEnd ? end : dayEnd;
            if (stop <= start)
            {
                continue;
            }

            var startSecond = LocalSecond(zone, start);
            // Al acabar justo en el cambio de día el reloj local marcaría 0: se usa el final del eje. En el día en que
            // se retrasa el reloj, el final local podría quedar antes que el inicio: se recorta a una franja vacía.
            var endSecond = stop == dayEnd ? SecondsPerDay : Math.Max(LocalSecond(zone, stop), startSecond);
            segments.Add(new TimelineSegment(kind.Value, startSecond, endSecond, (int)(stop - start).TotalSeconds, item.ClockOutAt is null));
        }

        return new DayTimeline(date, SpanishDates.IsWeekend(date), date == zone.DateOf(now), [.. segments.OrderBy(segment => segment.StartSecond)]);
    }

    private static int LocalSecond(ILocalTimeZone zone, DateTimeOffset instant)
        => (int)(instant.UtcDateTime + zone.OffsetAt(instant)).TimeOfDay.TotalSeconds;
}
