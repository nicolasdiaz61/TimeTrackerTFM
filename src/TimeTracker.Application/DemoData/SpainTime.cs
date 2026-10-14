namespace TimeTracker.Application.DemoData;

/// <summary>
/// Hora local de España peninsular con la regla de la UE: UTC+2 desde las 01:00Z del último domingo de marzo hasta las
/// 01:00Z del último domingo de octubre, y UTC+1 el resto del año. No usa la base de datos de zonas horarias del
/// sistema porque la imagen de ejecución (Alpine) no la incluye.
/// </summary>
public static class SpainTime
{
    private static readonly TimeSpan WinterOffset = TimeSpan.FromHours(1);
    private static readonly TimeSpan SummerOffset = TimeSpan.FromHours(2);
    private static readonly TimeSpan ChangeTime = TimeSpan.FromHours(1);

    /// <summary>Convierte una fecha y hora locales a UTC; las horas ambiguas o inexistentes del cambio se resuelven con el desfase de invierno o verano según su instante.</summary>
    public static DateTimeOffset ToUtc(DateOnly date, TimeOnly time)
    {
        // Instante si fuera invierno: decide si, en ese instante UTC, rige el horario de verano.
        var asWinter = new DateTimeOffset(date.ToDateTime(time), TimeSpan.Zero) - WinterOffset;
        return IsSummer(asWinter) ? asWinter - (SummerOffset - WinterOffset) : asWinter;
    }

    /// <summary>Fecha local de España en el instante UTC indicado.</summary>
    public static DateOnly DateOf(DateTimeOffset utc)
        => DateOnly.FromDateTime((utc + (IsSummer(utc) ? SummerOffset : WinterOffset)).DateTime);

    private static bool IsSummer(DateTimeOffset utc)
    {
        var start = LastSunday(utc.Year, month: 3) + ChangeTime;
        var end = LastSunday(utc.Year, month: 10) + ChangeTime;
        return utc >= new DateTimeOffset(start, TimeSpan.Zero) && utc < new DateTimeOffset(end, TimeSpan.Zero);
    }

    private static DateTime LastSunday(int year, int month)
    {
        var lastDay = new DateTime(year, month, DateTime.DaysInMonth(year, month));
        return lastDay.AddDays(-(int)lastDay.DayOfWeek);
    }
}
