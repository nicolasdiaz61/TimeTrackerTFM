using System.Globalization;

using TimeTracker.Application.TimeTracking;

namespace TimeTracker.Api.TimeEntries;

/// <summary>Lectura de los parámetros opcionales <c>from</c> y <c>to</c> de un listado de fichajes.</summary>
public static class TimeWindowQuery
{
    // Solo ISO 8601 con segundos: sin desfase se interpreta como UTC y con desfase («Z» o «+02:00») se normaliza.
    private static readonly string[] Formats = ["yyyy-MM-dd'T'HH:mm:ssK", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK"];

    /// <summary>
    /// Devuelve <c>null</c> si los parámetros son válidos (con <paramref name="window"/> nula cuando no se pidió ventana)
    /// o el motivo en español del rechazo.
    /// </summary>
    public static string? TryRead(string? from, string? to, DateTimeOffset now, out TimeWindow? window)
    {
        window = null;
        if (string.IsNullOrEmpty(from) && string.IsNullOrEmpty(to))
        {
            return null;
        }

        if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to))
        {
            return "Indique el inicio y el fin del periodo (from y to), o ninguno de los dos.";
        }

        if (!TryParse(from, out var start) || !TryParse(to, out var end))
        {
            return "Las fechas deben tener formato ISO 8601, por ejemplo 2026-10-01T00:00:00Z.";
        }

        return TimeWindow.TryCreate(start, end, now, out window, out var error) ? null : error;
    }

    private static bool TryParse(string text, out DateTimeOffset value)
        => DateTimeOffset.TryParseExact(text, Formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out value);
}
