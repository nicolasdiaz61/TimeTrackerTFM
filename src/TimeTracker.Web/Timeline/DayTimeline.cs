namespace TimeTracker.Web.Timeline;

/// <summary>Un día local con sus tramos ordenados por inicio y sus totales.</summary>
public sealed record DayTimeline(DateOnly Date, bool IsWeekend, bool IsToday, IReadOnlyList<TimelineSegment> Segments)
{
    public bool HasEntries => Segments.Count > 0;

    public int WorkedMinutes => MinutesOf(SegmentKind.Work);

    public int BreakMinutes => MinutesOf(SegmentKind.Break);

    public int MealMinutes => MinutesOf(SegmentKind.MealBreak);

    /// <summary>Segundos trabajados (incluye el tramo abierto hasta «ahora»); lo usa el cronómetro del Inicio.</summary>
    public int WorkedSeconds => Segments.Where(segment => segment.Kind == SegmentKind.Work).Sum(segment => segment.DurationSeconds);

    private int MinutesOf(SegmentKind kind) => Segments.Where(segment => segment.Kind == kind).Sum(segment => segment.DurationMinutes);
}
