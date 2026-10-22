using TimeTracker.Domain.WorkSchedules;

namespace TimeTracker.Application.WorkSchedules;

/// <summary>Una jornada con el número de personas que la tienen asignada.</summary>
public sealed record WorkScheduleView(WorkSchedule Schedule, int AssignedCount);
