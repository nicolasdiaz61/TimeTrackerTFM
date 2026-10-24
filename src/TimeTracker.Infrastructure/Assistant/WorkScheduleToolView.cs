using System.Globalization;
using System.Text.Json.Serialization;

using TimeTracker.Application.TimeTracking;
using TimeTracker.Domain.WorkSchedules;

namespace TimeTracker.Infrastructure.Assistant;

/// <summary>
/// Jornada tal como se entrega al modelo: horas «HH:mm», días en español (de lunes a domingo) y el nombre saneado porque lo
/// escribe un usuario. <see cref="AssignedCount"/> solo se rellena en el listado de la compañía.
/// </summary>
internal sealed record WorkScheduleToolView(
    string Name,
    string StartTime,
    string LunchStart,
    int LunchMinutes,
    int ExpectedBreakMinutes,
    IReadOnlyList<string> Days,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? AssignedCount = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? EndTime = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? ExpectedWorkMinutes = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ExpectedWorkText = null)
{
    private const string TimeFormat = "HH':'mm";

    public static WorkScheduleToolView From(WorkSchedule schedule, int? assignedCount = null)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        return new(
            DisplayNameSanitizer.Sanitize(schedule.Name),
            schedule.StartTime.ToString(TimeFormat, CultureInfo.InvariantCulture),
            schedule.LunchStart.ToString(TimeFormat, CultureInfo.InvariantCulture),
            schedule.LunchMinutes,
            schedule.ExpectedBreakMinutes,
            [.. schedule.Days.Select(DayName)],
            assignedCount,
            schedule.EndTime?.ToString(TimeFormat, CultureInfo.InvariantCulture),
            schedule.ExpectedWorkMinutes,
            schedule.ExpectedWorkMinutes is { } minutes ? DurationText.Format(minutes) : null);
    }

    private static string DayName(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "lunes",
        DayOfWeek.Tuesday => "martes",
        DayOfWeek.Wednesday => "miércoles",
        DayOfWeek.Thursday => "jueves",
        DayOfWeek.Friday => "viernes",
        DayOfWeek.Saturday => "sábado",
        _ => "domingo",
    };
}
