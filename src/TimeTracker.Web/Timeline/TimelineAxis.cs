namespace TimeTracker.Web.Timeline;

/// <summary>
/// Eje horario común a todas las filas de un mes, en horas enteras. La barra se dibuja en un SVG de
/// <see cref="ViewWidth"/> unidades de ancho; <see cref="X"/> y <see cref="Layout"/> convierten segundos a esas unidades.
/// </summary>
public sealed record TimelineAxis
{
    public const double ViewWidth = 1000;

    /// <summary>Ancho mínimo de una franja para que una pausa de unos minutos siga siendo visible.</summary>
    public const double MinSegmentWidth = 2;

    private const int SecondsPerHour = 3600;
    private const int WideAxisHours = 18;

    public TimelineAxis(int startHour, int endHour)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(startHour, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(endHour, 24);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(startHour, endHour);
        StartHour = startHour;
        EndHour = endHour;
    }

    /// <summary>Eje mínimo, de 06:00 a 22:00.</summary>
    public static TimelineAxis Default { get; } = new(6, 22);

    public int StartHour { get; }

    public int EndHour { get; }

    /// <summary>Horas con marca: cada 2 h, o cada 3 h si el eje supera 18 h.</summary>
    public IReadOnlyList<int> TickHours
    {
        get
        {
            var step = EndHour - StartHour > WideAxisHours ? 3 : 2;
            var first = (StartHour + step - 1) / step * step;
            return [.. Enumerable.Range(0, ((EndHour - first) / step) + 1).Select(index => first + (index * step))];
        }
    }

    /// <summary>Posición horizontal (0 a <see cref="ViewWidth"/>) de un segundo de reloj local; fuera del eje se recorta.</summary>
    public double X(int second)
    {
        var fraction = (double)(second - (StartHour * SecondsPerHour)) / ((EndHour - StartHour) * SecondsPerHour);
        return Math.Clamp(fraction, 0, 1) * ViewWidth;
    }

    /// <summary>Posición y ancho de la franja; nunca más estrecha que <see cref="MinSegmentWidth"/> ni fuera del dibujo.</summary>
    public (double X, double Width) Layout(TimelineSegment segment)
    {
        var start = X(segment.StartSecond);
        var width = Math.Max(MinSegmentWidth, X(segment.EndSecond) - start);
        return (Math.Min(start, ViewWidth - width), width);
    }
}
