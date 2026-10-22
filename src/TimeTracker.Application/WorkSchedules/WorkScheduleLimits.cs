namespace TimeTracker.Application.WorkSchedules;

public static class WorkScheduleLimits
{
    /// <summary>Tope de jornadas por compañía, para no abrir un cauce de abuso.</summary>
    public const int MaxPerCompany = 20;
}
