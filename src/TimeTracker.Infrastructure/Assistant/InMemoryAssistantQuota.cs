using System.Collections.Concurrent;

using Microsoft.Extensions.Options;

using TimeTracker.Application.Assistant;
using TimeTracker.Application.Time;

namespace TimeTracker.Infrastructure.Assistant;

/// <summary>
/// Tope diario de consultas por empleado (DD-2), en memoria: el día es el natural de España y se reinicia a su
/// medianoche. El contador vive en cada instancia: se pierde al reiniciar y no se comparte entre réplicas.
/// </summary>
public sealed class InMemoryAssistantQuota(IOptions<AiOptions> options, TimeProvider timeProvider) : IAssistantQuota
{
    private readonly ConcurrentDictionary<Guid, (DateOnly Day, int Count)> _counts = new();

    public QuotaDecision TryConsume(Guid employeeId)
    {
        var now = timeProvider.GetUtcNow();
        var today = SpainTime.DateOf(now);
        var limit = options.Value.DailyRequestsPerUser;

        // Bucle de comparación e intercambio: el incremento es atómico y nunca supera el tope.
        while (true)
        {
            if (!_counts.TryGetValue(employeeId, out var current))
            {
                if (_counts.TryAdd(employeeId, (today, 1)))
                {
                    return new QuotaDecision(true, TimeSpan.Zero);
                }

                continue;
            }

            var used = current.Day == today ? current.Count : 0;
            if (used >= limit)
            {
                return new QuotaDecision(false, SpainTime.StartOfDay(today.AddDays(1)) - now);
            }

            if (_counts.TryUpdate(employeeId, (today, used + 1), current))
            {
                return new QuotaDecision(true, TimeSpan.Zero);
            }
        }
    }
}
