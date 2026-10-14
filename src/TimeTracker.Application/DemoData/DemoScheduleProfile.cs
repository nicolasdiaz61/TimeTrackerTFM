namespace TimeTracker.Application.DemoData;

/// <summary>
/// Forma de trabajar de una persona de demostración: semilla del generador, desfase de sus días de ausencia,
/// variación diaria (minutos) de entrada y salida, jornada habitual y jornadas distintas por día de la semana.
/// </summary>
public sealed record DemoScheduleProfile(
    ulong Seed,
    int AbsenceOffset,
    int Variation,
    DemoDayPlan Standard,
    IReadOnlyDictionary<DayOfWeek, DemoDayPlan>? Overrides = null)
{
    public DemoDayPlan PlanFor(DayOfWeek day)
        => Overrides is not null && Overrides.TryGetValue(day, out var plan) ? plan : Standard;
}
