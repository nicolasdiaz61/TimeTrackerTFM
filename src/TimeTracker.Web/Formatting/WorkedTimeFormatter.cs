using System.Globalization;

namespace TimeTracker.Web.Formatting;

/// <summary>Presentación del total trabajado: solo da formato al dato que devuelve la API, no calcula nada.</summary>
public static class WorkedTimeFormatter
{
    private const int MinutesPerHour = 60;

    /// <summary>Texto como «1 minuto (0:01)» o «90 minutos (1:30)».</summary>
    public static string FormatTotal(int totalMinutes)
    {
        var noun = totalMinutes == 1 ? "minuto" : "minutos";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{totalMinutes} {noun} ({totalMinutes / MinutesPerHour}:{totalMinutes % MinutesPerHour:00})");
    }
}
