using System.Collections.Concurrent;

using Microsoft.Extensions.Options;

using TimeTracker.Application.Assistant;
using TimeTracker.Application.Time;

namespace TimeTracker.Infrastructure.Assistant;

/// <summary>
/// Topes del asistente en memoria (DD-2, DD-16): el día es el natural de Europe/Madrid y se reinicia a su medianoche; el minuto es
/// una ventana fija por compañía. Los contadores viven en cada instancia: se pierden al reiniciar y no se comparten entre réplicas.
/// Todo el estado de una compañía (su contador y los de sus personas) se muta bajo un único cerrojo propio, así que el tope de una
/// compañía nunca bloquea a otra y las dos comprobaciones de <see cref="TryConsumeDaily"/> son atómicas.
/// </summary>
public sealed class InMemoryAssistantQuota(IOptions<AiOptions> options, TimeProvider timeProvider) : IAssistantQuota
{
    private static readonly TimeSpan MinuteWindow = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<Guid, CompanyDay> _days = new();
    private readonly ConcurrentDictionary<Guid, MinuteCount> _minutes = new();
    private int _lastSweptDay;

    /// <summary>Compañías con contador diario vigente (para comprobar la purga de días anteriores).</summary>
    internal int TrackedCompanies => _days.Count;

    public QuotaDecision TryConsumeMinute(Guid companyId)
    {
        var now = timeProvider.GetUtcNow();
        var limit = options.Value.RequestsPerMinutePerCompany;
        var window = _minutes.GetOrAdd(companyId, _ => new MinuteCount());
        lock (window)
        {
            if (window.Count == 0 || now - window.Start >= MinuteWindow)
            {
                window.Start = now;
                window.Count = 0;
            }

            if (window.Count >= limit)
            {
                return new QuotaDecision(false, window.Start + MinuteWindow - now, QuotaScope.CompanyMinute);
            }

            window.Count++;
            return new QuotaDecision(true, TimeSpan.Zero, QuotaScope.CompanyMinute);
        }
    }

    public QuotaDecision TryConsumeDaily(Guid companyId, Guid employeeId)
    {
        var now = timeProvider.GetUtcNow();
        var today = SpainTime.DateOf(now);
        SweepOnce(today);

        while (true)
        {
            var company = _days.GetOrAdd(companyId, _ => new CompanyDay(today));
            lock (company)
            {
                if (company.Removed)
                {
                    // La purga la retiró entre la búsqueda y el cerrojo: se vuelve a buscar o crear.
                    continue;
                }

                if (company.Day != today)
                {
                    company.Day = today;
                    company.Count = 0;
                    company.Users.Clear();
                }

                company.Users.TryGetValue(employeeId, out var used);
                var tomorrow = SpainTime.StartOfDay(today.AddDays(1)) - now;
                if (used >= options.Value.DailyRequestsPerUser)
                {
                    return new QuotaDecision(false, tomorrow, QuotaScope.User);
                }

                if (company.Count >= options.Value.DailyRequestsPerCompany)
                {
                    return new QuotaDecision(false, tomorrow, QuotaScope.CompanyDaily);
                }

                company.Users[employeeId] = used + 1;
                company.Count++;
                return new QuotaDecision(true, TimeSpan.Zero, QuotaScope.User);
            }
        }
    }

    // Un único barrido al cambiar de día: el primer hilo que ve el día nuevo gana el intercambio y retira los contadores del anterior.
    private void SweepOnce(DateOnly today)
    {
        var day = today.DayNumber;
        var previous = Volatile.Read(ref _lastSweptDay);
        if (previous == day || Interlocked.CompareExchange(ref _lastSweptDay, day, previous) != previous)
        {
            return;
        }

        foreach (var (companyId, company) in _days)
        {
            lock (company)
            {
                if (company.Day != today)
                {
                    company.Removed = true;
                    _days.TryRemove(companyId, out _);
                }
            }
        }
    }

    private sealed class CompanyDay(DateOnly day)
    {
        public DateOnly Day { get; set; } = day;

        public int Count { get; set; }

        public bool Removed { get; set; }

        public Dictionary<Guid, int> Users { get; } = [];
    }

    private sealed class MinuteCount
    {
        public DateTimeOffset Start { get; set; }

        public int Count { get; set; }
    }
}
