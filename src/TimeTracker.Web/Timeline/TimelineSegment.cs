namespace TimeTracker.Web.Timeline;

/// <summary>
/// Tramo de un intervalo dentro de un día local. Las posiciones son segundos de reloj local desde las 00:00
/// (0 a 86400); la duración es tiempo real transcurrido, de modo que en los días de cambio de hora pueden diferir.
/// </summary>
public sealed record TimelineSegment(SegmentKind Kind, int StartSecond, int EndSecond, int DurationSeconds, bool IsOpen)
{
    private const int SecondsPerMinute = 60;

    /// <summary>Minutos enteros del tramo (se trunca por tramo; los totales suman estos valores).</summary>
    public int DurationMinutes => DurationSeconds / SecondsPerMinute;
}
