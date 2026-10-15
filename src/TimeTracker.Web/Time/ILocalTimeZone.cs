namespace TimeTracker.Web.Time;

/// <summary>
/// Zona horaria del usuario. La API solo habla UTC; la Web necesita saber qué hora y qué día es un instante para esa
/// persona y cuándo empieza su día. La implementación real consulta al navegador (ver <see cref="BrowserTimeZone"/>).
/// </summary>
public interface ILocalTimeZone
{
    /// <summary>Desfase local respecto a UTC en el instante indicado (incluye el horario de verano).</summary>
    TimeSpan OffsetAt(DateTimeOffset instant);

    /// <summary>Instante UTC en el que empieza el día local <paramref name="date"/> (las 00:00 locales).</summary>
    DateTimeOffset StartOfDay(DateOnly date);
}

public static class LocalTimeZoneExtensions
{
    /// <summary>Fecha local en la que cae el instante.</summary>
    public static DateOnly DateOf(this ILocalTimeZone zone, DateTimeOffset instant)
        => DateOnly.FromDateTime((instant.UtcDateTime + zone.OffsetAt(instant)));
}
