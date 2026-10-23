namespace TimeTracker.Web.WorkSchedules;

// Contrato de /api/work-schedules (docs/api/openapi.json). DTO duplicado a propósito (sin referencias a src/).
// La Web nunca envía un identificador de compañía: sale del token. Los días son los números de DayOfWeek (0 = domingo).
public sealed record WorkScheduleDto(
    Guid Id,
    string Name,
    TimeOnly StartTime,
    TimeOnly LunchStart,
    int LunchMinutes,
    int ExpectedBreakMinutes,
    DayOfWeek[] Days,
    int AssignedCount);

public sealed record WorkScheduleListDto(IReadOnlyList<WorkScheduleDto> Items);

public sealed record WorkScheduleRequestDto(
    string Name,
    TimeOnly? StartTime,
    TimeOnly? LunchStart,
    int? LunchMinutes,
    int? ExpectedBreakMinutes,
    DayOfWeek[] Days);

public sealed record AssignWorkScheduleRequestDto(Guid? WorkScheduleId);

/// <summary>Textos en español de una jornada, sin dependencia del navegador.</summary>
public static class WorkSchedulePresentation
{
    // De lunes a domingo, el orden natural de una jornada en España.
    public static readonly IReadOnlyList<DayOfWeek> WeekOrder =
    [
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday,
    ];

    private static readonly string[] LongNames = ["domingo", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado"];

    public static string DayName(DayOfWeek day) => LongNames[(int)day];

    public static string DaysLabel(IEnumerable<DayOfWeek> days)
        => string.Join(", ", WeekOrder.Where(days.Contains).Select(DayName));

    /// <summary>Una línea como «Oficina: entrada 09:00, almuerzo 14:00 (60 min), pausa esperada 30 min, lunes, martes».</summary>
    public static string Summary(WorkScheduleDto schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        return $"{schedule.Name}: entrada {Time(schedule.StartTime)}, almuerzo {Time(schedule.LunchStart)} ({schedule.LunchMinutes} min), "
            + $"pausa esperada {schedule.ExpectedBreakMinutes} min, {DaysLabel(schedule.Days)}";
    }

    public static string Time(TimeOnly time) => time.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
}
