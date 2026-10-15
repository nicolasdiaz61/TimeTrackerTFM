using System.Globalization;

using TimeTracker.Web.Calendar;
using TimeTracker.Web.Formatting;

namespace TimeTracker.Web.Timeline;

/// <summary>Textos en español de las franjas y de los días: tooltips, etiquetas accesibles y totales cortos.</summary>
public static class TimelineText
{
    private const int SecondsPerDay = 86400;
    private const int SecondsPerHour = 3600;
    private const int SecondsPerMinute = 60;

    /// <summary>Hora «HH:mm» de un segundo de reloj local; el final de día (24:00) se lee «00:00».</summary>
    public static string TimeOfDay(int second)
    {
        var inDay = second % SecondsPerDay;
        return string.Create(CultureInfo.InvariantCulture, $"{inDay / SecondsPerHour:00}:{inDay / SecondsPerMinute % SecondsPerMinute:00}");
    }

    /// <summary>Texto de una franja, como «Comida de 13:00 a 14:00, 1 hora».</summary>
    public static string SegmentText(TimelineSegment segment)
    {
        var kind = SegmentKinds.Label(segment.Kind);
        var duration = segment.DurationMinutes == 0 ? "menos de un minuto" : DurationFormatter.Spoken(segment.DurationMinutes);
        return segment.IsOpen
            ? $"{kind} en curso desde las {TimeOfDay(segment.StartSecond)}, {duration}"
            : $"{kind} de {TimeOfDay(segment.StartSecond)} a {TimeOfDay(segment.EndSecond)}, {duration}";
    }

    /// <summary>Descripción completa de un día para lectores de pantalla.</summary>
    public static string DayText(DayTimeline day)
    {
        var label = SpanishDates.LongDayLabel(day.Date);
        if (!day.HasEntries)
        {
            return $"{label}: sin fichajes.";
        }

        return $"{label}: {string.Join("; ", day.Segments.Select(SegmentText))}. "
            + $"Trabajado {DurationFormatter.Spoken(day.WorkedMinutes)}, pausas {DurationFormatter.Spoken(day.BreakMinutes)}, comidas {DurationFormatter.Spoken(day.MealMinutes)}.";
    }

    /// <summary>Total trabajado del día en forma corta.</summary>
    public static string WorkedLabel(DayTimeline day) => DurationFormatter.Compact(day.WorkedMinutes);

    /// <summary>Pausas y comidas del día en forma corta.</summary>
    public static string OthersLabel(DayTimeline day)
        => $"Pausas {DurationFormatter.Compact(day.BreakMinutes)} · Comidas {DurationFormatter.Compact(day.MealMinutes)}";
}
