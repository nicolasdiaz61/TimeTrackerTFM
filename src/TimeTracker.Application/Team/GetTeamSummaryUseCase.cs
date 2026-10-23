using System.Globalization;

using TimeTracker.Application.Identity;
using TimeTracker.Application.TimeTracking;
using TimeTracker.Application.Users;
using TimeTracker.Application.WorkSchedules;
using TimeTracker.Domain.TimeTracking;

namespace TimeTracker.Application.Team;

/// <summary>
/// Resume y compara los fichajes de los reportes directos del usuario actual en un periodo. No recibe identificadores
/// de compañía ni de miembro: el equipo sale siempre de <see cref="ICurrentUser"/>. Hace una consulta de fichajes por
/// miembro, aceptable con el tope de <see cref="MaxMembers"/>, y una sola de jornadas de la compañía.
/// </summary>
public sealed class GetTeamSummaryUseCase(IUserRepository users, ITimeEntryRepository entries, IWorkScheduleRepository schedules, ICurrentUser currentUser)
{
    // Más bajo que GetTeamMembersUseCase.MaxMembers: cada miembro cuesta una consulta de fichajes.
    public const int MaxMembers = 50;

    public const double DefaultDailyHours = 8;
    public const double MinDailyHours = 1;
    public const double MaxDailyHours = 12;

    private const int RoundingDigits = 1;
    private const int MinutesPerHour = 60;

    // Sin jornada asignada se supone lunes a viernes.
    private static readonly IReadOnlySet<DayOfWeek> DefaultWorkingDays = new HashSet<DayOfWeek>
    {
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday,
    };

    /// <summary>
    /// Las horas extra de cada día local son lo trabajado por encima de <paramref name="dailyHours"/> si el día es laborable
    /// en la jornada de la persona (lunes a viernes si no tiene), o todo lo trabajado si no lo es. La jornada no guarda una
    /// duración prevista, por eso es una suposición explícita que se devuelve en el resultado.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="dailyHours"/> fuera de 1 a 12.</exception>
    public async Task<TeamSummaryResult> ExecuteAsync(LocalDateRange range, DateTimeOffset now, double dailyHours, CancellationToken cancellationToken)
    {
        if (double.IsNaN(dailyHours) || dailyHours is < MinDailyHours or > MaxDailyHours)
        {
            throw new ArgumentOutOfRangeException(nameof(dailyHours), dailyHours, "Las horas diarias deben estar entre 1 y 12.");
        }

        var expectedMinutes = (int)Math.Round(dailyHours * MinutesPerHour);

        // Se pide uno más para saber si el equipo supera el tope sin devolver una lista sin cota.
        var reports = await users.ListDirectReportsAsync(currentUser.EmployeeId, MaxMembers + 1, cancellationToken);
        var truncated = reports.Count > MaxMembers;
        var window = range.ToWindow();

        // Una sola consulta de jornadas de la compañía del contexto, y solo si algún miembro tiene una asignada.
        var workingDays = new Dictionary<Guid, IReadOnlySet<DayOfWeek>>();
        if (reports.Any(report => report.WorkScheduleId is not null))
        {
            foreach (var schedule in await schedules.ListAsync(cancellationToken))
            {
                workingDays[schedule.Id] = schedule.Days.ToHashSet();
            }
        }

        var totals = new List<(Guid Id, string Name, DailySummaryResult Summary, int BreakCount, int MealBreakCount, int Overtime, int OvertimeDays)>();
        foreach (var report in reports.Take(MaxMembers))
        {
            var found = await entries.ListInWindowAsync(report.Id, window, GetTimeEntriesUseCase.MaxWindowEntries, cancellationToken);
            var list = TimeEntryListResult.From(found, open: null);
            var closed = found.Where(entry => entry.ClockOutAt is not null).ToList();
            var summary = DailySummaryBuilder.Build(list, range, now);
            var working = report.WorkScheduleId is { } scheduleId && workingDays.TryGetValue(scheduleId, out var days) ? days : DefaultWorkingDays;
            var overtime = summary.Days
                .Select(day => Math.Max(0, day.WorkedMinutes - (working.Contains(day.Date.DayOfWeek) ? expectedMinutes : 0)))
                .ToList();
            totals.Add((
                report.Id,
                report.DisplayName,
                summary,
                closed.Count(entry => entry.Type == TimeEntryType.Break),
                closed.Count(entry => entry.Type == TimeEntryType.MealBreak),
                overtime.Sum(),
                overtime.Count(minutes => minutes > 0)));
        }

        var count = totals.Count;
        var averages = new TeamAverages(
            Average(totals.Sum(member => member.Summary.WorkedMinutes), count),
            Average(totals.Sum(member => member.Summary.BreakMinutes), count),
            Average(totals.Sum(member => member.BreakCount), count),
            Average(totals.Sum(member => member.Summary.MealBreakMinutes), count),
            Average(totals.Sum(member => member.Overtime), count));

        var members = totals
            .Select(member => new TeamMemberSummary(
                member.Id,
                member.Name,
                member.Summary.WorkedMinutes,
                member.Summary.BreakMinutes,
                member.Summary.MealBreakMinutes,
                member.BreakCount,
                member.MealBreakCount,
                member.Summary.Days.Count,
                Round(member.Summary.BreakMinutes - averages.BreakMinutes),
                Round(member.BreakCount - averages.BreakCount),
                member.Summary.BreakMinutes > averages.BreakMinutes,
                member.BreakCount > averages.BreakCount,
                member.Summary.Open,
                member.Summary.Truncated,
                member.Overtime,
                member.OvertimeDays,
                Round(member.Overtime - averages.OvertimeMinutes),
                member.Overtime > averages.OvertimeMinutes))
            .OrderByDescending(member => member.BreakMinutes)
            .ThenBy(member => member.Name, StringComparer.Ordinal)
            .ToList();

        var note = string.Create(
            CultureInfo.InvariantCulture,
            $"Horas extra = trabajo por encima de {dailyHours} h en días laborables de su jornada (lunes a viernes si no tiene) y todo el trabajo en días no laborables.");
        return new TeamSummaryResult(range.From, range.To, count, truncated, Rounded(averages), members, dailyHours, note);
    }

    private static double Average(int sum, int count) => count == 0 ? 0 : (double)sum / count;

    private static double Round(double value) => Math.Round(value, RoundingDigits);

    private static TeamAverages Rounded(TeamAverages averages)
        => new(Round(averages.WorkedMinutes), Round(averages.BreakMinutes), Round(averages.BreakCount), Round(averages.MealBreakMinutes), Round(averages.OvertimeMinutes));
}
