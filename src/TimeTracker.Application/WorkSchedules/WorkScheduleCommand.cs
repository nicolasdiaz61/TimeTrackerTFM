using TimeTracker.Domain.WorkSchedules;

namespace TimeTracker.Application.WorkSchedules;

/// <summary>Datos de una jornada tal como llegan del cliente; ningún campo lleva la compañía (sale de <c>ICurrentUser</c>).</summary>
public sealed record WorkScheduleCommand(
    string? Name,
    TimeOnly? StartTime,
    TimeOnly? LunchStart,
    int? LunchMinutes,
    int? ExpectedBreakMinutes,
    IReadOnlyCollection<DayOfWeek>? Days,
    TimeOnly? EndTime = null)
{
    internal WorkScheduleDetails ToDetails() => new(Name, StartTime, LunchStart, LunchMinutes, ExpectedBreakMinutes, Days, EndTime);
}
