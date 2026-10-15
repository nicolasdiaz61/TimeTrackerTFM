using TimeTracker.Web.Time;

namespace TimeTracker.Web.Tests.Support;

/// <summary>Zona sin cambio de hora (por ejemplo UTC o UTC-05:00) para probar sin depender del calendario.</summary>
internal sealed class FixedOffsetTimeZone(TimeSpan offset) : ILocalTimeZone
{
    public static FixedOffsetTimeZone Utc { get; } = new(TimeSpan.Zero);

    public TimeSpan OffsetAt(DateTimeOffset instant) => offset;

    public DateTimeOffset StartOfDay(DateOnly date)
        => new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) - offset;
}
