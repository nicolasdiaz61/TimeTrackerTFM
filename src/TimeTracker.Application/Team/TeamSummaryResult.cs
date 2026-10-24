using TimeTracker.Application.TimeTracking;

namespace TimeTracker.Application.Team;

/// <summary>
/// Resumen comparativo del equipo en un periodo: todo está calculado en código (totales, medias, diferencias y horas
/// extra) para que el modelo no sume, divida ni compare. Las horas extra usan las horas de la jornada de cada persona cuando
/// tiene hora de salida y, si no, una jornada diaria supuesta (<see cref="AssumedDailyHours"/>, explicada en <see cref="OvertimeNote"/>). Los miembros van ordenados por minutos de
/// pausa descendente y luego por nombre.
/// </summary>
public sealed record TeamSummaryResult(
    DateOnly From,
    DateOnly To,
    int MemberCount,
    bool MembersTruncated,
    TeamAverages Averages,
    IReadOnlyList<TeamMemberSummary> Members,
    double AssumedDailyHours,
    string OvertimeNote);

/// <summary>Medias por miembro del equipo (un miembro sin fichajes cuenta como cero); 0 si el equipo está vacío.</summary>
public sealed record TeamAverages(double WorkedMinutes, double BreakMinutes, double BreakCount, double MealBreakMinutes, double OvertimeMinutes)
{
    public string WorkedText => DurationText.Format((int)Math.Round(WorkedMinutes));

    public string BreakText => DurationText.Format((int)Math.Round(BreakMinutes));

    public string MealBreakText => DurationText.Format((int)Math.Round(MealBreakMinutes));

    public string OvertimeText => DurationText.Format((int)Math.Round(OvertimeMinutes));
}

/// <summary>
/// Totales de un miembro en el periodo (solo intervalos cerrados; el abierto va aparte en <see cref="Open"/>) y su
/// posición respecto a la media del equipo. <see cref="EntriesTruncated"/> indica que alcanzó el tope de intervalos.
/// </summary>
public sealed record TeamMemberSummary(
    Guid MemberId,
    string Name,
    int WorkedMinutes,
    int BreakMinutes,
    int MealBreakMinutes,
    int BreakCount,
    int MealBreakCount,
    int DaysWithEntries,
    double BreakMinutesVsAverage,
    double BreakCountVsAverage,
    bool AboveAverageBreakMinutes,
    bool AboveAverageBreakCount,
    OpenEntrySummary? Open,
    bool EntriesTruncated,
    int OvertimeMinutes,
    int OvertimeDays,
    double OvertimeVsAverage,
    bool AboveAverageOvertime,
    string ExpectedBasis = TeamMemberSummary.JornadaBasis,
    int ExpectedDailyMinutes = 0)
{
    /// <summary>Las horas previstas por día salen de la hora de salida de la jornada de la persona.</summary>
    public const string JornadaBasis = "jornada";

    /// <summary>La persona no tiene jornada con hora de salida: se usa la suposición de horas diarias.</summary>
    public const string AssumedBasis = "supuesto";

    public string WorkedText => DurationText.Format(WorkedMinutes);

    public string BreakText => DurationText.Format(BreakMinutes);

    public string MealBreakText => DurationText.Format(MealBreakMinutes);

    public string OvertimeText => DurationText.Format(OvertimeMinutes);
}
