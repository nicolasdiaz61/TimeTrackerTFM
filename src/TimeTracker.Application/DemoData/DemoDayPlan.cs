namespace TimeTracker.Application.DemoData;

/// <summary>Jornada local nominal de un día de demostración: entrada, salida y pausas previstas en orden cronológico.</summary>
public sealed record DemoDayPlan(TimeOnly Start, TimeOnly End, IReadOnlyList<DemoBreakPlan> Breaks);
