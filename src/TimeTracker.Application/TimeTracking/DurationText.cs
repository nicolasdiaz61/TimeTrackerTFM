using System.Globalization;

namespace TimeTracker.Application.TimeTracking;

/// <summary>Texto «X h Y min» de una duración en minutos: lo precalcula el servidor para que el asistente no tenga que convertir.</summary>
public static class DurationText
{
    private const int MinutesPerHour = 60;

    public static string Format(int minutes)
        => string.Create(CultureInfo.InvariantCulture, $"{minutes / MinutesPerHour} h {minutes % MinutesPerHour} min");
}
