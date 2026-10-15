using TimeTracker.Web.Time;
using TimeTracker.Web.TimeEntries;

namespace TimeTracker.Web.Timeline;

/// <summary>
/// Lo que muestra el cronómetro del reproductor en un instante: el tiempo del tramo en curso (sin recortar al día,
/// desde que empezó el intervalo abierto) y el total trabajado hoy. Se calcula siempre desde las marcas del servidor,
/// así que recargar la página no reinicia la cuenta.
/// </summary>
public sealed record PlayerSnapshot(int CurrentSegmentSeconds, int WorkedTodaySeconds)
{
    public static PlayerSnapshot From(IReadOnlyList<TimeEntryDto> items, ILocalTimeZone zone, DateTimeOffset now)
    {
        var open = items.FirstOrDefault(item => item.ClockOutAt is null);
        var current = open is null ? 0 : Math.Max(0, (int)(now - open.ClockInAt).TotalSeconds);
        var today = DayTimelineBuilder.Build(zone.DateOf(now), items, zone, now);

        return new PlayerSnapshot(current, today.WorkedSeconds);
    }
}
