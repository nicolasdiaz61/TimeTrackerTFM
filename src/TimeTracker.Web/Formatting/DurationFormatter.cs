using System.Globalization;

namespace TimeTracker.Web.Formatting;

/// <summary>Formato en español de duraciones (corto para la pantalla, hablado para lectores de pantalla) y del cronómetro.</summary>
public static class DurationFormatter
{
    private const int MinutesPerHour = 60;
    private const int SecondsPerMinute = 60;

    /// <summary>Forma corta: «0 min», «45 min», «1 h», «1 h 5 min», «2 h 30 min».</summary>
    public static string Compact(int totalMinutes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(totalMinutes);
        var (hours, minutes) = Math.DivRem(totalMinutes, MinutesPerHour);
        return (hours, minutes) switch
        {
            (0, _) => string.Create(CultureInfo.InvariantCulture, $"{minutes} min"),
            (_, 0) => string.Create(CultureInfo.InvariantCulture, $"{hours} h"),
            _ => string.Create(CultureInfo.InvariantCulture, $"{hours} h {minutes} min"),
        };
    }

    /// <summary>Forma hablada con plurales: «1 minuto», «1 hora y 5 minutos», «2 horas».</summary>
    public static string Spoken(int totalMinutes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(totalMinutes);
        var (hours, minutes) = Math.DivRem(totalMinutes, MinutesPerHour);
        var hoursText = string.Create(CultureInfo.InvariantCulture, $"{hours} {(hours == 1 ? "hora" : "horas")}");
        var minutesText = string.Create(CultureInfo.InvariantCulture, $"{minutes} {(minutes == 1 ? "minuto" : "minutos")}");
        return (hours, minutes) switch
        {
            (0, _) => minutesText,
            (_, 0) => hoursText,
            _ => $"{hoursText} y {minutesText}",
        };
    }

    /// <summary>Cronómetro «HH:MM:SS»; los valores negativos (reloj desajustado) se muestran como cero.</summary>
    public static string Stopwatch(long totalSeconds)
    {
        var seconds = Math.Max(0, totalSeconds);
        var hours = seconds / (MinutesPerHour * SecondsPerMinute);
        var minutes = seconds / SecondsPerMinute % MinutesPerHour;
        return string.Create(CultureInfo.InvariantCulture, $"{hours:00}:{minutes:00}:{seconds % SecondsPerMinute:00}");
    }
}
