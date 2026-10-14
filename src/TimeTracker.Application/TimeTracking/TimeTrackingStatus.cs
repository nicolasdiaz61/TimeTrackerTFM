namespace TimeTracker.Application.TimeTracking;

/// <summary>Situación actual de un empleado, deducida de su intervalo abierto (si lo hay).</summary>
public enum TimeTrackingStatus
{
    Idle = 0,
    Working = 1,
    OnBreak = 2,
    OnMealBreak = 3,
}
