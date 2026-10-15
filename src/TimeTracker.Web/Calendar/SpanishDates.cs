namespace TimeTracker.Web.Calendar;

/// <summary>
/// Nombres de meses y de días en español. Se codifican porque WebAssembly se ejecuta con cultura invariante y no
/// traduciría «October» ni «Wednesday».
/// </summary>
public static class SpanishDates
{
    private static readonly string[] Months =
    [
        "enero", "febrero", "marzo", "abril", "mayo", "junio",
        "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre",
    ];

    // Indexados por DayOfWeek (domingo = 0).
    private static readonly string[] ShortWeekdays = ["dom", "lun", "mar", "mié", "jue", "vie", "sáb"];

    private static readonly string[] LongWeekdays = ["domingo", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado"];

    public static string MonthName(int month) => Months[month - 1];

    /// <summary>Texto como «octubre de 2026».</summary>
    public static string MonthLabel(int year, int month) => $"{MonthName(month)} de {year}";

    /// <summary>Texto corto como «mié 14».</summary>
    public static string DayLabel(DateOnly date) => $"{ShortWeekdays[(int)date.DayOfWeek]} {date.Day}";

    /// <summary>Texto completo como «miércoles 14», para lectores de pantalla.</summary>
    public static string LongDayLabel(DateOnly date) => $"{LongWeekdays[(int)date.DayOfWeek]} {date.Day}";

    public static bool IsWeekend(DateOnly date) => date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
}
