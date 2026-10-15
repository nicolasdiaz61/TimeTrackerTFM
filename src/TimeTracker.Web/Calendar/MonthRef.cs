using TimeTracker.Web.Time;

namespace TimeTracker.Web.Calendar;

/// <summary>Un mes natural (año y mes) y la navegación entre meses; sin meses futuros respecto al actual.</summary>
public readonly record struct MonthRef
{
    private const int MonthsPerYear = 12;

    public MonthRef(int year, int month)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(month, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(month, MonthsPerYear);
        Year = year;
        Month = month;
    }

    public int Year { get; }

    public int Month { get; }

    public DateOnly FirstDay => new(Year, Month, 1);

    public DateOnly LastDay => new(Year, Month, DaysInMonth);

    public int DaysInMonth => DateTime.DaysInMonth(Year, Month);

    /// <summary>Texto como «octubre de 2026».</summary>
    public string Label => SpanishDates.MonthLabel(Year, Month);

    public static MonthRef Of(DateOnly date) => new(date.Year, date.Month);

    public MonthRef Previous() => Month == 1 ? new MonthRef(Year - 1, MonthsPerYear) : new MonthRef(Year, Month - 1);

    public MonthRef Next() => Month == MonthsPerYear ? new MonthRef(Year + 1, 1) : new MonthRef(Year, Month + 1);

    public bool IsCurrent(MonthRef current) => this == current;

    /// <summary>Se puede avanzar solo mientras el mes siguiente no sea posterior al actual.</summary>
    public bool CanGoNext(MonthRef current)
    {
        var next = Next();
        return next.Year < current.Year || (next.Year == current.Year && next.Month <= current.Month);
    }

    /// <summary>
    /// Ventana semiabierta <c>[from, to)</c> en UTC: las 00:00 locales del primer día del mes y del primer día del siguiente.
    /// El desfase real de cada borde (cambio de hora de marzo y octubre) lo resuelve la zona.
    /// </summary>
    public (DateTimeOffset From, DateTimeOffset To) Window(ILocalTimeZone zone)
        => (zone.StartOfDay(FirstDay), zone.StartOfDay(Next().FirstDay));
}
