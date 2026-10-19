using TimeTracker.Domain.TimeTracking;

namespace TimeTracker.Application.TimeTracking;

/// <summary>
/// Actividad de un día local: primer inicio, último fin (como máximo 23:59) y minutos por tipo de los tramos cerrados.
/// Los textos «X h Y min» van precalculados para que el modelo no convierta minutos.
/// </summary>
public sealed record DaySummary(DateOnly Date, TimeOnly FirstStart, TimeOnly LastEnd, int WorkedMinutes, int BreakMinutes, int MealBreakMinutes)
{
    public string WorkedText => DurationText.Format(WorkedMinutes);

    public string BreakText => DurationText.Format(BreakMinutes);

    public string MealBreakText => DurationText.Format(MealBreakMinutes);
}

/// <summary>Intervalo abierto (no suma en los totales): tipo, inicio local y minutos transcurridos hasta «ahora».</summary>
public sealed record OpenEntrySummary(TimeEntryType Type, DateOnly SinceDate, TimeOnly SinceTime, int ElapsedMinutes);

/// <summary>Resumen del periodo; los totales de minutos y sus textos «X h Y min» se calculan en código, el modelo no suma.</summary>
public sealed record DailySummaryResult(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<DaySummary> Days,
    int WorkedMinutes,
    int BreakMinutes,
    int MealBreakMinutes,
    TimeTrackingStatus Status,
    OpenEntrySummary? Open,
    bool Truncated)
{
    public string WorkedText => DurationText.Format(WorkedMinutes);

    public string BreakText => DurationText.Format(BreakMinutes);

    public string MealBreakText => DurationText.Format(MealBreakMinutes);
}
