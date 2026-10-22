namespace TimeTracker.Application.WorkSchedules;

public enum AssignWorkScheduleOutcome
{
    Assigned,

    /// <summary>La persona no es del equipo del Manager o la jornada no existe en su compañía: indistinguibles.</summary>
    NotFound,

    /// <summary>La fila de la persona cambió mientras tanto (concurrencia optimista).</summary>
    Conflict,
}
