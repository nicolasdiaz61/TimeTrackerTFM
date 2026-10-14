namespace TimeTracker.Domain.TimeTracking;

/// <summary>Naturaleza de un intervalo: solo <see cref="Work"/> cuenta como tiempo trabajado.</summary>
/// <remarks>Se guarda como texto (nombre del miembro); los valores numéricos no se persisten.</remarks>
public enum TimeEntryType
{
    Work = 0,
    Break = 1,
    MealBreak = 2,
}
