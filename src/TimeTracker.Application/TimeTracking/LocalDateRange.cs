using System.Diagnostics.CodeAnalysis;

using TimeTracker.Application.Time;

namespace TimeTracker.Application.TimeTracking;

/// <summary>
/// Periodo de fechas locales de España (ambas incluidas) de hasta <see cref="MaxDays"/> días. El asistente razona en
/// fechas y el servidor las convierte a instantes en la medianoche local, que nunca es ambigua: los días de cambio de
/// hora duran 23 o 25 horas.
/// </summary>
public sealed record LocalDateRange
{
    public const int MaxDays = 31;

    private LocalDateRange(DateOnly from, DateOnly to)
    {
        From = from;
        To = to;
    }

    public DateOnly From { get; }

    public DateOnly To { get; }

    /// <summary>Valida y crea el periodo; un fin posterior a <paramref name="today"/> se recorta a hoy antes de contar los días.</summary>
    public static bool TryCreate(
        DateOnly from,
        DateOnly to,
        DateOnly today,
        [NotNullWhen(true)] out LocalDateRange? range,
        [NotNullWhen(false)] out string? error)
    {
        range = null;
        error = null;
        if (to < from)
        {
            error = "La fecha de fin no puede ser anterior a la de inicio.";
        }
        else if (from > today)
        {
            error = "No se pueden consultar periodos futuros.";
        }
        else if (Math.Min(to.DayNumber, today.DayNumber) - from.DayNumber + 1 > MaxDays)
        {
            error = $"El periodo no puede superar los {MaxDays} días.";
        }
        else
        {
            range = new LocalDateRange(from, to > today ? today : to);
        }

        return range is not null;
    }

    /// <summary>Ventana <c>[inicio del primer día, inicio del día siguiente al último)</c>; 31 días locales nunca superan <see cref="TimeWindow.MaxDays"/>.</summary>
    public TimeWindow ToWindow()
    {
        var end = SpainTime.StartOfDay(To.AddDays(1));
        return TimeWindow.TryCreate(SpainTime.StartOfDay(From), end, now: end, out var window, out var error)
            ? window
            : throw new InvalidOperationException(error);
    }
}
