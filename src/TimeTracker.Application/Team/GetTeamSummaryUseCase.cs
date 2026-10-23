using TimeTracker.Application.Identity;
using TimeTracker.Application.TimeTracking;
using TimeTracker.Application.Users;
using TimeTracker.Domain.TimeTracking;

namespace TimeTracker.Application.Team;

/// <summary>
/// Resume y compara los fichajes de los reportes directos del usuario actual en un periodo. No recibe identificadores
/// de compañía ni de miembro: el equipo sale siempre de <see cref="ICurrentUser"/>. Hace una consulta de fichajes por
/// miembro, aceptable con el tope de <see cref="MaxMembers"/>.
/// </summary>
public sealed class GetTeamSummaryUseCase(IUserRepository users, ITimeEntryRepository entries, ICurrentUser currentUser)
{
    // Más bajo que GetTeamMembersUseCase.MaxMembers: cada miembro cuesta una consulta de fichajes.
    public const int MaxMembers = 50;

    private const int RoundingDigits = 1;

    public async Task<TeamSummaryResult> ExecuteAsync(LocalDateRange range, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // Se pide uno más para saber si el equipo supera el tope sin devolver una lista sin cota.
        var reports = await users.ListDirectReportsAsync(currentUser.EmployeeId, MaxMembers + 1, cancellationToken);
        var truncated = reports.Count > MaxMembers;
        var window = range.ToWindow();

        var totals = new List<(Guid Id, string Name, DailySummaryResult Summary, int BreakCount, int MealBreakCount)>();
        foreach (var report in reports.Take(MaxMembers))
        {
            var found = await entries.ListInWindowAsync(report.Id, window, GetTimeEntriesUseCase.MaxWindowEntries, cancellationToken);
            var list = TimeEntryListResult.From(found, open: null);
            var closed = found.Where(entry => entry.ClockOutAt is not null).ToList();
            totals.Add((
                report.Id,
                report.DisplayName,
                DailySummaryBuilder.Build(list, range, now),
                closed.Count(entry => entry.Type == TimeEntryType.Break),
                closed.Count(entry => entry.Type == TimeEntryType.MealBreak)));
        }

        var count = totals.Count;
        var averages = new TeamAverages(
            Average(totals.Sum(member => member.Summary.WorkedMinutes), count),
            Average(totals.Sum(member => member.Summary.BreakMinutes), count),
            Average(totals.Sum(member => member.BreakCount), count),
            Average(totals.Sum(member => member.Summary.MealBreakMinutes), count));

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
                member.Summary.Truncated))
            .OrderByDescending(member => member.BreakMinutes)
            .ThenBy(member => member.Name, StringComparer.Ordinal)
            .ToList();

        return new TeamSummaryResult(range.From, range.To, count, truncated, Rounded(averages), members);
    }

    private static double Average(int sum, int count) => count == 0 ? 0 : (double)sum / count;

    private static double Round(double value) => Math.Round(value, RoundingDigits);

    private static TeamAverages Rounded(TeamAverages averages)
        => new(Round(averages.WorkedMinutes), Round(averages.BreakMinutes), Round(averages.BreakCount), Round(averages.MealBreakMinutes));
}
