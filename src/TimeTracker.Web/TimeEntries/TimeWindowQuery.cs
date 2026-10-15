using System.Globalization;

namespace TimeTracker.Web.TimeEntries;

/// <summary>Parámetros de consulta <c>from</c> y <c>to</c> de la API: instantes UTC con sufijo «Z» (ventana semiabierta).</summary>
public static class TimeWindowQuery
{
    public static string Build(DateTimeOffset from, DateTimeOffset to)
        => $"from={Format(from)}&to={Format(to)}";

    private static string Format(DateTimeOffset instant)
        => Uri.EscapeDataString(instant.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
}
