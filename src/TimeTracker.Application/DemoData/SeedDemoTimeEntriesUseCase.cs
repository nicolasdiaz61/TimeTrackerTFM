using TimeTracker.Application.Time;
using TimeTracker.Domain.TimeTracking;

namespace TimeTracker.Application.DemoData;

/// <summary>
/// Siembra los fichajes de demostración de los últimos <see cref="WindowDays"/> días hasta ayer (fecha local de España;
/// hoy queda libre para fichar en vivo). Es idempotente por día y empleado: un día con algún intervalo ya existente se omite.
/// </summary>
public sealed class SeedDemoTimeEntriesUseCase(IDemoTimeEntryStore store, TimeProvider clock)
{
    public const int WindowDays = 30;

    public async Task<DemoSeedResult> ExecuteAsync(IReadOnlyList<DemoEmployee> employees, CancellationToken cancellationToken)
    {
        var today = SpainTime.DateOf(clock.GetUtcNow());
        var firstDay = today.AddDays(-WindowDays);
        var from = SpainTime.ToUtc(firstDay, TimeOnly.MinValue);
        var to = SpainTime.ToUtc(today, TimeOnly.MinValue);

        var created = new List<TimeEntry>();
        var createdDays = 0;
        var skippedDays = 0;
        foreach (var employee in employees)
        {
            var starts = await store.ListStartsBetweenAsync(employee.Id, from, to, cancellationToken);
            var occupiedDays = starts.Select(SpainTime.DateOf).ToHashSet();
            for (var day = firstDay; day < today; day = day.AddDays(1))
            {
                var entries = DemoScheduleGenerator.GenerateDay(employee.CompanyId, employee.Id, employee.Profile, day);
                if (entries.Count == 0)
                {
                    continue;
                }

                if (occupiedDays.Contains(day))
                {
                    skippedDays++;
                    continue;
                }

                created.AddRange(entries);
                createdDays++;
            }
        }

        if (created.Count > 0)
        {
            await store.AddRangeAsync(created, cancellationToken);
        }

        return new DemoSeedResult(createdDays, skippedDays, created.Count);
    }
}
