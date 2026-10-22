namespace TimeTracker.Application.WorkSchedules;

/// <summary>El almacén rechazó la jornada por nombre repetido en la compañía (índice único); cubre la carrera entre dos altas.</summary>
public sealed class WorkScheduleNameTakenException : Exception
{
    public WorkScheduleNameTakenException()
        : base("Ya existe una jornada con ese nombre en la compañía.")
    {
    }
}

/// <summary>El almacén rechazó el borrado porque hay personas con la jornada asignada (clave foránea).</summary>
public sealed class WorkScheduleInUseException : Exception
{
    public WorkScheduleInUseException()
        : base("La jornada está asignada a alguna persona.")
    {
    }
}
