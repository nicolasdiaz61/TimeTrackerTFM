using System.Diagnostics.CodeAnalysis;

namespace TimeTracker.Application.TimeTracking;

/// <summary>
/// Ventana semiabierta <c>[From, To)</c> en UTC para acotar un listado de fichajes. La vista mensual de la Web envía
/// un mes natural en hora local (28 a 31 días, más o menos una hora por el cambio de hora).
/// </summary>
public sealed record TimeWindow
{
    /// <summary>Máximo de días de una ventana: un mes más margen; es la protección principal frente a listados sin cota.</summary>
    public const int MaxDays = 32;

    private TimeWindow(DateTimeOffset from, DateTimeOffset to)
    {
        From = from;
        To = to;
    }

    public DateTimeOffset From { get; }

    public DateTimeOffset To { get; }

    /// <summary>Valida y crea la ventana; si no es válida devuelve el motivo en español.</summary>
    public static bool TryCreate(
        DateTimeOffset from,
        DateTimeOffset to,
        DateTimeOffset now,
        [NotNullWhen(true)] out TimeWindow? window,
        [NotNullWhen(false)] out string? error)
    {
        window = null;
        error = null;
        if (to <= from)
        {
            error = "El fin del periodo debe ser posterior al inicio.";
        }
        else if (to - from > TimeSpan.FromDays(MaxDays))
        {
            error = $"El periodo no puede superar los {MaxDays} días.";
        }
        else if (from > now)
        {
            error = "No se pueden consultar periodos futuros.";
        }
        else
        {
            window = new TimeWindow(from.ToUniversalTime(), to.ToUniversalTime());
        }

        return window is not null;
    }
}
