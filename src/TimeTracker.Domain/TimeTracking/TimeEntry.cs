namespace TimeTracker.Domain.TimeTracking;

/// <summary>
/// Fichaje de un empleado: una entrada y, cuando se cierra, una salida.
/// </summary>
/// <remarks>
/// Recibe instantes y nunca lee el reloj. El constructor privado con parámetros lo usan tanto
/// <see cref="Start"/> como EF Core (enlace por constructor).
/// </remarks>
public sealed class TimeEntry
{
    private TimeEntry(Guid id, Guid employeeId, DateTimeOffset clockInAt, DateTimeOffset? clockOutAt)
    {
        Id = id;
        EmployeeId = employeeId;
        ClockInAt = clockInAt;
        ClockOutAt = clockOutAt;
    }

    public Guid Id { get; }

    public Guid EmployeeId { get; }

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

    // El cast a int trunca (nunca redondea); `checked` evita desbordes silenciosos.
    public WorkedHours? CalculateWorked()
        => IsOpen ? null : WorkedHours.FromMinutes(checked((int)(ClockOutAt!.Value - ClockInAt).TotalMinutes));

    public static TimeEntry Start(Guid employeeId, DateTimeOffset at)
    {
        if (employeeId == Guid.Empty)
        {
            throw new ArgumentException("El identificador del empleado no puede estar vacío.", nameof(employeeId));
        }

        var truncated = ToUtcSeconds(at, nameof(at));
        return new(Guid.CreateVersion7(truncated), employeeId, truncated, null);
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
