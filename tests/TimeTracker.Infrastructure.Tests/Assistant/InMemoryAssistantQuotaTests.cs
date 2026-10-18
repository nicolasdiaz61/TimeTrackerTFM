using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using TimeTracker.Application.Time;
using TimeTracker.Infrastructure.Assistant;

namespace TimeTracker.Infrastructure.Tests.Assistant;

public sealed class InMemoryAssistantQuotaTests
{
    private static readonly Guid Ana = Guid.Parse("00000000-0000-4000-8000-00000000000b");
    private static readonly Guid Luis = Guid.Parse("00000000-0000-4000-8000-00000000000c");

    private readonly FakeTimeProvider _clock = new();

    private InMemoryAssistantQuota CreateQuota(int limit)
        => new(Options.Create(new AiOptions { DailyRequestsPerUser = limit }), _clock);

    private void SetLocal(DateOnly date, TimeOnly time) => _clock.SetUtcNow(SpainTime.ToUtc(date, time));

    [Fact]
    public void TryConsume_AtTheLimit_AllowsThemAndRejectsTheNext()
    {
        SetLocal(new DateOnly(2026, 10, 18), new TimeOnly(10, 0));
        var quota = CreateQuota(3);

        var decisions = Enumerable.Range(0, 4).Select(_ => quota.TryConsume(Ana)).ToList();

        Assert.All(decisions.Take(3), decision => Assert.True(decision.Allowed));
        Assert.False(decisions[3].Allowed);
    }

    [Fact]
    public void TryConsume_WhenAllowed_ReportsNoWait()
    {
        SetLocal(new DateOnly(2026, 10, 18), new TimeOnly(10, 0));

        Assert.Equal(TimeSpan.Zero, CreateQuota(3).TryConsume(Ana).RetryAfter);
    }

    [Fact]
    public void TryConsume_IsIndependentPerUser()
    {
        SetLocal(new DateOnly(2026, 10, 18), new TimeOnly(10, 0));
        var quota = CreateQuota(1);
        quota.TryConsume(Ana);

        Assert.False(quota.TryConsume(Ana).Allowed);
        Assert.True(quota.TryConsume(Luis).Allowed);
    }

    [Fact]
    public void TryConsume_RejectedAt2200_WaitsUntilLocalMidnight()
    {
        SetLocal(new DateOnly(2026, 10, 18), new TimeOnly(22, 0));
        var quota = CreateQuota(1);
        quota.TryConsume(Ana);

        var decision = quota.TryConsume(Ana);

        Assert.False(decision.Allowed);
        Assert.Equal(TimeSpan.FromSeconds(7200), decision.RetryAfter);
    }

    [Fact]
    public void TryConsume_RejectedOnALongDay_WaitsUntilTheRealLocalMidnight()
    {
        // 2026-10-25 dura 25 horas: a las 00:30 locales faltan 24,5 horas para el 26.
        SetLocal(new DateOnly(2026, 10, 25), new TimeOnly(0, 30));
        var quota = CreateQuota(1);
        quota.TryConsume(Ana);

        Assert.Equal(TimeSpan.FromHours(24.5), quota.TryConsume(Ana).RetryAfter);
    }

    [Fact]
    public void TryConsume_AfterLocalMidnight_StartsANewDay()
    {
        SetLocal(new DateOnly(2026, 10, 18), new TimeOnly(23, 59));
        var quota = CreateQuota(1);
        quota.TryConsume(Ana);
        Assert.False(quota.TryConsume(Ana).Allowed);

        _clock.Advance(TimeSpan.FromMinutes(2));

        Assert.True(quota.TryConsume(Ana).Allowed);
        Assert.False(quota.TryConsume(Ana).Allowed);
    }

    [Fact]
    public void TryConsume_UsesTheSpanishDayNotTheUtcDay()
    {
        // 22:30Z del 18 de octubre ya es 00:30 del día 19 en España: otro día natural.
        _clock.SetUtcNow(new DateTimeOffset(2026, 10, 18, 21, 30, 0, TimeSpan.Zero));
        var quota = CreateQuota(1);
        quota.TryConsume(Ana);

        _clock.SetUtcNow(new DateTimeOffset(2026, 10, 18, 22, 30, 0, TimeSpan.Zero));

        Assert.True(quota.TryConsume(Ana).Allowed);
    }

    [Fact]
    public async Task TryConsume_UnderConcurrency_NeverExceedsTheLimit()
    {
        SetLocal(new DateOnly(2026, 10, 18), new TimeOnly(10, 0));
        var quota = CreateQuota(10);

        var results = await Task.WhenAll(Enumerable.Range(0, 200)
            .Select(_ => Task.Run(() => quota.TryConsume(Ana).Allowed)));

        Assert.Equal(10, results.Count(allowed => allowed));
    }
}
