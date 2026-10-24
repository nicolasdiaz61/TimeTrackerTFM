namespace TimeTracker.Domain.WorkSchedules;

/// <summary>Datos de una jornada tal como llegan del cliente: nulos si faltan, para poder validarlos todos a la vez.</summary>
public sealed record WorkScheduleDetails(
    string? Name,
    TimeOnly? StartTime,
    TimeOnly? LunchStart,
    int? LunchMinutes,
    int? ExpectedBreakMinutes,
    IReadOnlyCollection<DayOfWeek>? Days,
    TimeOnly? EndTime = null);

public sealed record WorkScheduleError(string Field, string Message);

/// <summary>
/// Jornada de trabajo de una compañía: hora de entrada, hora de salida (opcional), almuerzo, pausa esperada por día y días de la semana en que aplica.
/// Las horas son locales (sin zona). Los días se guardan como máscara de bits (bit = <see cref="DayOfWeek"/>).
/// </summary>
public sealed class WorkSchedule
{
    public const int MaxNameLength = 100;

    public const int MaxMinutes = 480;

    private const int AllDaysMask = 0b111_1111;

    private WorkSchedule(
        Guid id,
        Guid companyId,
        string name,
        TimeOnly startTime,
        TimeOnly lunchStart,
        int lunchMinutes,
        int expectedBreakMinutes,
        int daysMask,
        TimeOnly? endTime)
    {
        Id = id;
        CompanyId = companyId;
        Name = name;
        StartTime = startTime;
        LunchStart = lunchStart;
        LunchMinutes = lunchMinutes;
        ExpectedBreakMinutes = expectedBreakMinutes;
        DaysMask = daysMask;
        EndTime = endTime;
    }

    public Guid Id { get; }

    public Guid CompanyId { get; }

    public string Name { get; private set; }

    public TimeOnly StartTime { get; private set; }

    public TimeOnly LunchStart { get; private set; }

    public int LunchMinutes { get; private set; }

    public int ExpectedBreakMinutes { get; private set; }

    public int DaysMask { get; private set; }

    /// <summary>Hora de salida; nula en las jornadas anteriores a que existiera este dato.</summary>
    public TimeOnly? EndTime { get; private set; }

    /// <summary>Minutos de trabajo esperados por día (salida - entrada - almuerzo); nulo sin hora de salida.</summary>
    public int? ExpectedWorkMinutes => EndTime is { } end ? WorkedMinutes(StartTime, end, LunchMinutes) : null;

    /// <summary>Días en que aplica, ordenados de lunes a domingo.</summary>
    public IReadOnlyList<DayOfWeek> Days => FromMask(DaysMask);

    public static WorkSchedule Create(Guid id, Guid companyId, WorkScheduleDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        if (id == Guid.Empty)
        {
            throw new ArgumentException("El identificador de la jornada no puede estar vacío.", nameof(id));
        }

        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("La compañía de la jornada no puede estar vacía.", nameof(companyId));
        }

        RequireValid(details);
        return new(
            id, companyId, details.Name!.Trim(), details.StartTime!.Value, details.LunchStart!.Value,
            details.LunchMinutes!.Value, details.ExpectedBreakMinutes!.Value, ToMask(details.Days!), details.EndTime);
    }

    public void Update(WorkScheduleDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        RequireValid(details);
        Name = details.Name!.Trim();
        StartTime = details.StartTime!.Value;
        LunchStart = details.LunchStart!.Value;
        LunchMinutes = details.LunchMinutes!.Value;
        ExpectedBreakMinutes = details.ExpectedBreakMinutes!.Value;
        DaysMask = ToMask(details.Days!);
        EndTime = details.EndTime;
    }

    public static IReadOnlyList<WorkScheduleError> Validate(WorkScheduleDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        var errors = new List<WorkScheduleError>();

        if (details.Name is null || details.Name.Trim().Length is < 1 or > MaxNameLength)
        {
            errors.Add(new("name", $"El nombre debe tener entre 1 y {MaxNameLength} caracteres."));
        }

        if (details.StartTime is null)
        {
            errors.Add(new("startTime", "La hora de entrada es obligatoria."));
        }

        if (details.LunchStart is null)
        {
            errors.Add(new("lunchStart", "La hora de inicio del almuerzo es obligatoria."));
        }
        else if (details.StartTime is { } start && details.LunchStart.Value <= start)
        {
            errors.Add(new("lunchStart", "El almuerzo debe empezar después de la hora de entrada."));
        }

        if (details.LunchMinutes is not (>= 0 and <= MaxMinutes))
        {
            errors.Add(new("lunchMinutes", $"La duración del almuerzo debe estar entre 0 y {MaxMinutes} minutos."));
        }
        else if (details.LunchStart is { } lunch && lunch.ToTimeSpan().TotalMinutes + details.LunchMinutes.Value >= 24 * 60)
        {
            errors.Add(new("lunchMinutes", "El almuerzo debe terminar antes de medianoche."));
        }

        if (details.ExpectedBreakMinutes is not (>= 0 and <= MaxMinutes))
        {
            errors.Add(new("expectedBreakMinutes", $"La pausa esperada debe estar entre 0 y {MaxMinutes} minutos."));
        }

        if (details.Days is not { Count: > 0 } days || days.Any(day => !Enum.IsDefined(day)))
        {
            errors.Add(new("days", "Seleccione al menos un día de la semana."));
        }

        AddEndTimeErrors(details, errors);
        return errors;
    }

    public static int ToMask(IEnumerable<DayOfWeek> days) => days.Aggregate(0, (mask, day) => mask | (1 << (int)day)) & AllDaysMask;

    private static int WorkedMinutes(TimeOnly start, TimeOnly end, int lunchMinutes)
        => (int)(end.ToTimeSpan() - start.ToTimeSpan()).TotalMinutes - lunchMinutes;

    private static void AddEndTimeErrors(WorkScheduleDetails details, List<WorkScheduleError> errors)
    {
        if (details.EndTime is not { } end || details.StartTime is not { } start)
        {
            return;
        }

        if (end <= start)
        {
            errors.Add(new("endTime", "La hora de salida debe ser posterior a la hora de entrada."));
        }
        else if (details.LunchStart is { } lunchStart && details.LunchMinutes is >= 0 and <= MaxMinutes
            && end.ToTimeSpan() <= lunchStart.ToTimeSpan() + TimeSpan.FromMinutes(details.LunchMinutes.Value))
        {
            errors.Add(new("endTime", "La hora de salida debe ser posterior al fin del almuerzo."));
        }
        else if (details.LunchMinutes is >= 0 and <= MaxMinutes && WorkedMinutes(start, end, details.LunchMinutes.Value) < 1)
        {
            errors.Add(new("endTime", "La jornada debe tener al menos un minuto de trabajo."));
        }
    }

    private static void RequireValid(WorkScheduleDetails details)
    {
        if (Validate(details) is { Count: > 0 } errors)
        {
            throw new ArgumentException(string.Join(" ", errors.Select(error => error.Message)), nameof(details));
        }
    }

    private static List<DayOfWeek> FromMask(int mask)
    {
        // De lunes a domingo, el orden natural de una jornada en España.
        DayOfWeek[] order = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];
        return [.. order.Where(day => (mask & (1 << (int)day)) != 0)];
    }
}
