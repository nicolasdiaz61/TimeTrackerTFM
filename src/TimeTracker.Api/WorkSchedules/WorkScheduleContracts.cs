using TimeTracker.Application.WorkSchedules;
using TimeTracker.Domain.WorkSchedules;

namespace TimeTracker.Api.WorkSchedules;

/// <summary>
/// Cuerpo de alta y edición de una jornada. Las horas son locales (<c>HH:mm:ss</c>) y <c>days</c> usa los números de
/// <see cref="DayOfWeek"/> (0 = domingo ... 6 = sábado). <c>endTime</c> es opcional por compatibilidad con jornadas y clientes anteriores.
/// No lleva compañía: sale del token.
/// </summary>
public sealed record WorkScheduleRequest(
    string? Name,
    TimeOnly? StartTime,
    TimeOnly? LunchStart,
    int? LunchMinutes,
    int? ExpectedBreakMinutes,
    DayOfWeek[]? Days,
    TimeOnly? EndTime = null)
{
    public WorkScheduleCommand ToCommand() => new(Name, StartTime, LunchStart, LunchMinutes, ExpectedBreakMinutes, Days, EndTime);
}

/// <summary>Asignación de jornada a una persona del equipo; <c>null</c> la retira.</summary>
public sealed record AssignWorkScheduleRequest(Guid? WorkScheduleId);

public sealed record WorkScheduleResponse(
    Guid Id,
    string Name,
    TimeOnly StartTime,
    TimeOnly LunchStart,
    int LunchMinutes,
    int ExpectedBreakMinutes,
    DayOfWeek[] Days,
    int AssignedCount,
    TimeOnly? EndTime,
    int? ExpectedWorkMinutes)
{
    public static WorkScheduleResponse From(WorkScheduleView view) => From(view.Schedule, view.AssignedCount);

    public static WorkScheduleResponse From(WorkSchedule schedule, int assignedCount) => new(
        schedule.Id,
        schedule.Name,
        schedule.StartTime,
        schedule.LunchStart,
        schedule.LunchMinutes,
        schedule.ExpectedBreakMinutes,
        [.. schedule.Days],
        assignedCount,
        schedule.EndTime,
        schedule.ExpectedWorkMinutes);
}

public sealed record WorkScheduleListResponse(IReadOnlyList<WorkScheduleResponse> Items)
{
    public static WorkScheduleListResponse From(IReadOnlyList<WorkScheduleView> views) => new([.. views.Select(WorkScheduleResponse.From)]);
}
