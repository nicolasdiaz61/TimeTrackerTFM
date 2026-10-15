using TimeTracker.Web.TimeEntries;

namespace TimeTracker.Web.Tests.Support;

/// <summary>Fábrica de intervalos como los devuelve la API (UTC) para las pruebas de la vista.</summary>
internal static class Entries
{
    public static TimeEntryDto Work(DateTimeOffset start, DateTimeOffset? end) => Create("Work", start, end);

    public static TimeEntryDto Break(DateTimeOffset start, DateTimeOffset? end) => Create("Break", start, end);

    public static TimeEntryDto Meal(DateTimeOffset start, DateTimeOffset? end) => Create("MealBreak", start, end);

    public static TimeEntryDto Create(string type, DateTimeOffset start, DateTimeOffset? end)
        => new(Guid.NewGuid(), type, start, end, end is { } closed ? (int)(closed - start).TotalMinutes : null);

    public static DateTimeOffset Utc(int year, int month, int day, int hour = 0, int minute = 0, int second = 0)
        => new(year, month, day, hour, minute, second, TimeSpan.Zero);
}
