namespace TimeTracker.Domain.TimeTracking;

/// <summary>
/// Intervalo de un empleado: un inicio y, cuando se cierra, un fin, de un tipo (<see cref="TimeEntryType"/>).
/// </summary>
/// <remarks>
/// Recibe instantes y nunca lee el reloj. El constructor privado con parámetros lo usan tanto
/// <see cref="Start"/> como EF Core (enlace por constructor). Un empleado tiene como máximo un intervalo
/// abierto: <see cref="Pause"/> y <see cref="Resume"/> cierran el actual y devuelven el siguiente.
/// </remarks>
public sealed class TimeEntry
{
    private TimeEntry(Guid id, Guid companyId, Guid employeeId, TimeEntryType type, DateTimeOffset clockInAt, DateTimeOffset? clockOutAt)
    {
        Id = id;
        CompanyId = companyId;
        EmployeeId = employeeId;
        Type = type;
        ClockInAt = clockInAt;
        ClockOutAt = clockOutAt;
    }

    public Guid Id { get; }

    public Guid CompanyId { get; }

    public Guid EmployeeId { get; }

    public TimeEntryType Type { get; }

    public DateTimeOffset ClockInAt { get; }

    public DateTimeOffset? ClockOutAt { get; private set; }

    public bool IsOpen => ClockOutAt is null;

    public void Close(DateTimeOffset at)
    {
        if (!IsOpen)
        {
            throw new TimeEntryAlreadyClosedException();
        }

        var truncated = ToUtcSeconds(at, nameof(at));
        if (truncated < ClockInAt)
        {
            throw new ClockOutBeforeClockInException();
        }

        ClockOutAt = truncated;
    }

    /// <summary>Cierra un intervalo de trabajo y devuelve la pausa (<c>Break</c> o <c>MealBreak</c>) que empieza en <paramref name="at"/>.</summary>
    public TimeEntry Pause(TimeEntryType breakType, DateTimeOffset at)
    {
        if (Type != TimeEntryType.Work)
        {
            throw new NotWorkingException();
        }

        if (breakType is not (TimeEntryType.Break or TimeEntryType.MealBreak))
        {
            throw new ArgumentOutOfRangeException(nameof(breakType), breakType, "El tipo de pausa debe ser Break o MealBreak.");
        }

        return CloseAndStartNext(breakType, at);
    }

    /// <summary>Cierra una pausa y devuelve el intervalo de trabajo que empieza en <paramref name="at"/>.</summary>
    public TimeEntry Resume(DateTimeOffset at)
    {
        if (Type == TimeEntryType.Work)
        {
            throw new NoOpenBreakException();
        }

        return CloseAndStartNext(TimeEntryType.Work, at);
    }

    // Duración de cualquier tipo; que cuente como trabajo o como pausa lo decide el tipo.
    // El cast a int trunca (nunca redondea); `checked` evita desbordes silenciosos.
    public WorkedHours? CalculateDuration()
        => IsOpen ? null : WorkedHours.FromMinutes(checked((int)(ClockOutAt!.Value - ClockInAt).TotalMinutes));

    public static TimeEntry Start(Guid companyId, Guid employeeId, TimeEntryType type, DateTimeOffset at)
    {
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("La compañía no puede estar vacía.", nameof(companyId));
        }

        if (employeeId == Guid.Empty)
        {
            throw new ArgumentException("El identificador del empleado no puede estar vacío.", nameof(employeeId));
        }

        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "El tipo de intervalo no está definido.");
        }

        var truncated = ToUtcSeconds(at, nameof(at));
        return new(Guid.CreateVersion7(truncated), companyId, employeeId, type, truncated, null);
    }

    // El siguiente intervalo empieza justo donde acaba este (ya truncado), sin hueco ni solape.
    private TimeEntry CloseAndStartNext(TimeEntryType nextType, DateTimeOffset at)
    {
        Close(at);
        return Start(CompanyId, EmployeeId, nextType, ClockOutAt!.Value);
    }

    // `timestamptz` guarda microsegundos: truncar a segundos enteros evita que el valor en memoria
    // y el leído de la base de datos difieran. El truncado a la baja conserva salida >= entrada.
    private static DateTimeOffset ToUtcSeconds(DateTimeOffset at, string paramName)
    {
        if (at.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("El instante debe estar en UTC (desfase cero).", paramName);
        }

        return at.AddTicks(-(at.Ticks % TimeSpan.TicksPerSecond));
    }
}
