using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using TimeTracker.Application.Assistant;
using TimeTracker.Application.Time;
using TimeTracker.Infrastructure.Assistant;

namespace TimeTracker.Infrastructure.Tests.Assistant;

public sealed class InMemoryAssistantQuotaTests
{
    private static readonly Guid Ana = Guid.Parse("00000000-0000-4000-8000-00000000000b");
    private static readonly Guid Luis = Guid.Parse("00000000-0000-4000-8000-00000000000c");

    private readonly FakeTimeProvider _clock = new();

    private static readonly Guid Acme = Guid.Parse("00000000-0000-4000-8000-0000000000a1");
    private static readonly Guid Beta = Guid.Parse("00000000-0000-4000-8000-0000000000b1");

    private InMemoryAssistantQuota CreateQuota(int limit)
        => CreateQuota(userLimit: limit, companyLimit: 1000);

    private InMemoryAssistantQuota CreateQuota(int userLimit = 1000, int companyLimit = 1000, int perMinute = 1000)
        => new(Options.Create(new AiOptions { DailyRequestsPerUser = userLimit, DailyRequestsPerCompany = companyLimit, RequestsPerMinutePerCompany = perMinute }), _clock);

    private void SetLocal(DateOnly date, TimeOnly time) => _clock.SetUtcNow(SpainTime.ToUtc(date, time));

    [Fact]
    public void TryConsumeDaily_AtTheLimit_AllowsThemAndRejectsTheNext()
    {
        SetLocal(new DateOnly(2026, 10, 18), new TimeOnly(10, 0));
        var quota = CreateQuota(3);

        var decisions = Enumerable.Range(0, 4).Select(_ => quota.TryConsumeDaily(Acme, Ana)).ToList();

        Assert.All(decisions.Take(3), decision => Assert.True(decision.Allowed));
        Assert.False(decisions[3].Allowed);
    }

    [Fact]
    public void TryConsumeDaily_WhenAllowed_ReportsNoWait()
    {
        SetLocal(new DateOnly(2026, 10, 18), new TimeOnly(10, 0));

        Assert.Equal(TimeSpan.Zero, CreateQuota(3).TryConsumeDaily(Acme, Ana).RetryAfter);
    }

    [Fact]
    public void TryConsumeDaily_IsIndependentPerUser()
    {
        SetLocal(new DateOnly(2026, 10, 18), new TimeOnly(10, 0));
        var quota = CreateQuota(1);
        quota.TryConsumeDaily(Acme, Ana);

        Assert.False(quota.TryConsumeDaily(Acme, Ana).Allowed);
        Assert.True(quota.TryConsumeDaily(Acme, Luis).Allowed);
    }

    [Fact]
    public void TryConsumeDaily_RejectedAt2200_WaitsUntilLocalMidnight()
    {
        SetLocal(new DateOnly(2026, 10, 18), new TimeOnly(22, 0));
        var quota = CreateQuota(1);
        quota.TryConsumeDaily(Acme, Ana);

        var decision = quota.TryConsumeDaily(Acme, Ana);

        Assert.False(decision.Allowed);
        Assert.Equal(TimeSpan.FromSeconds(7200), decision.RetryAfter);
    }

    [Fact]
    public void TryConsumeDaily_RejectedOnALongDay_WaitsUntilTheRealLocalMidnight()
    {
        // 2026-10-25 dura 25 horas: a las 00:30 locales faltan 24,5 horas para el 26.
        SetLocal(new DateOnly(2026, 10, 25), new TimeOnly(0, 30));
        var quota = CreateQuota(1);
        quota.TryConsumeDaily(Acme, Ana);

        Assert.Equal(TimeSpan.FromHours(24.5), quota.TryConsumeDaily(Acme, Ana).RetryAfter);
    }

    [Fact]
    public void TryConsumeDaily_AfterLocalMidnight_StartsANewDay()
    {
        SetLocal(new DateOnly(2026, 10, 18), new TimeOnly(23, 59));
        var quota = CreateQuota(1);
        quota.TryConsumeDaily(Acme, Ana);
        Assert.False(quota.TryConsumeDaily(Acme, Ana).Allowed);

        _clock.Advance(TimeSpan.FromMinutes(2));

        Assert.True(quota.TryConsumeDaily(Acme, Ana).Allowed);
        Assert.False(quota.TryConsumeDaily(Acme, Ana).Allowed);
    }

    [Fact]
    public void TryConsumeDaily_UsesTheSpanishDayNotTheUtcDay()
    {
        // 22:30Z del 18 de octubre ya es 00:30 del día 19 en España: otro día natural.
        _clock.SetUtcNow(new DateTimeOffset(2026, 10, 18, 21, 30, 0, TimeSpan.Zero));
        var quota = CreateQuota(1);
        quota.TryConsumeDaily(Acme, Ana);

        _clock.SetUtcNow(new DateTimeOffset(2026, 10, 18, 22, 30, 0, TimeSpan.Zero));

        Assert.True(quota.TryConsumeDaily(Acme, Ana).Allowed);
    }

    [Fact]
    public async Task TryConsumeDaily_UnderConcurrency_NeverExceedsTheLimit()
    {
        SetLocal(new DateOnly(2026, 10, 18), new TimeOnly(10, 0));
        var quota = CreateQuota(10);

        var results = await Task.WhenAll(Enumerable.Range(0, 200)
            .Select(_ => Task.Run(() => quota.TryConsumeDaily(Acme, Ana).Allowed)));

        Assert.Equal(10, results.Count(allowed => allowed));
    }

    // --- Tope diario y ritmo por minuto de la compañía (DD-16, TC-17) ---

    [Fact]
    public void TryConsumeDaily_CompanyLimitReached_RejectsOtherUsersOfThatCompanyOnly()
    {
        SetLocal(new DateOnly(2026, 10, 18), new TimeOnly(10, 0));
        var quota = CreateQuota(companyLimit: 3);
        var users = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToList();

        Assert.All(users, user => Assert.True(quota.TryConsumeDaily(Acme, user).Allowed));
        var rejected = quota.TryConsumeDaily(Acme, Guid.NewGuid());

        Assert.False(rejected.Allowed);
        Assert.Equal(QuotaScope.CompanyDaily, rejected.Scope);
        Assert.True(quota.TryConsumeDaily(Beta, Guid.NewGuid()).Allowed);
    }

    [Fact]
    public void TryConsumeDaily_UserLimitReached_ReportsTheUserScope()
    {
        SetLocal(new DateOnly(2026, 10, 18), new TimeOnly(10, 0));
        var quota = CreateQuota(userLimit: 1, companyLimit: 10);
        quota.TryConsumeDaily(Acme, Ana);

        var rejected = quota.TryConsumeDaily(Acme, Ana);

        Assert.False(rejected.Allowed);
        Assert.Equal(QuotaScope.User, rejected.Scope);
    }

    [Fact]
    public void TryConsumeDaily_RejectionByTheUser_DoesNotConsumeTheCompany()
    {
        SetLocal(new DateOnly(2026, 10, 18), new TimeOnly(10, 0));
        var quota = CreateQuota(userLimit: 1, companyLimit: 2);
        quota.TryConsumeDaily(Acme, Ana);
        Assert.False(quota.TryConsumeDaily(Acme, Ana).Allowed);
        Assert.False(quota.TryConsumeDaily(Acme, Ana).Allowed);

        // Si los rechazos hubieran consumido la compañía, este segundo usuario ya no cabría (tope 2).
        Assert.True(quota.TryConsumeDaily(Acme, Luis).Allowed);
    }

    [Fact]
    public void TryConsumeDaily_RejectionByTheCompany_DoesNotConsumeTheUser()
    {
        SetLocal(new DateOnly(2026, 10, 18), new TimeOnly(10, 0));
        var quota = CreateQuota(userLimit: 2, companyLimit: 1);
        quota.TryConsumeDaily(Acme, Luis);

        // Mismo día, sin purga: la comprobación de la persona va primero, así que si el rechazo de la compañía consumiera las unidades de
        // Ana, el tercer rechazo ya sería por su propio tope (alcanzado en 2) y no por el de la compañía.
        var rejections = Enumerable.Range(0, 3).Select(_ => quota.TryConsumeDaily(Acme, Ana)).ToList();

        Assert.All(rejections, rejected =>
        {
            Assert.False(rejected.Allowed);
            Assert.Equal(QuotaScope.CompanyDaily, rejected.Scope);
        });
    }

    [Fact]
    public void TryConsumeDaily_CompanyLimitAt2200_WaitsUntilLocalMidnight_AndResetsAfterIt()
    {
        SetLocal(new DateOnly(2026, 10, 18), new TimeOnly(22, 0));
        var quota = CreateQuota(companyLimit: 1);
        quota.TryConsumeDaily(Acme, Ana);

        var rejected = quota.TryConsumeDaily(Acme, Luis);

        Assert.Equal(TimeSpan.FromSeconds(7200), rejected.RetryAfter);
        _clock.Advance(TimeSpan.FromHours(2.1));
        Assert.True(quota.TryConsumeDaily(Acme, Luis).Allowed);
    }

    [Fact]
    public async Task TryConsumeDaily_CompanyLimitUnderConcurrency_NeverExceedsTheLimit()
    {
        SetLocal(new DateOnly(2026, 10, 18), new TimeOnly(10, 0));
        var quota = CreateQuota(companyLimit: 25);

        var results = await Task.WhenAll(Enumerable.Range(0, 300)
            .Select(_ => Task.Run(() => quota.TryConsumeDaily(Acme, Guid.NewGuid()).Allowed)));

        Assert.Equal(25, results.Count(allowed => allowed));
    }

    [Fact]
    public void TryConsumeMinute_AllowsTheLimitAndRejectsTheNextWithTheRestOfTheWindow()
    {
        SetLocal(new DateOnly(2026, 10, 18), new TimeOnly(10, 0));
        var quota = CreateQuota(perMinute: 3);

        Assert.All(Enumerable.Range(0, 3), _ => Assert.True(quota.TryConsumeMinute(Acme).Allowed));
        _clock.Advance(TimeSpan.FromSeconds(20));
        var rejected = quota.TryConsumeMinute(Acme);

        Assert.False(rejected.Allowed);
        Assert.Equal(QuotaScope.CompanyMinute, rejected.Scope);
        Assert.Equal(TimeSpan.FromSeconds(40), rejected.RetryAfter);
    }

    [Fact]
    public void TryConsumeMinute_IsIndependentPerCompany_AndFixedWindowResetsAfterAMinute()
    {
        SetLocal(new DateOnly(2026, 10, 18), new TimeOnly(10, 0));
        var quota = CreateQuota(perMinute: 1);
        quota.TryConsumeMinute(Acme);

        Assert.False(quota.TryConsumeMinute(Acme).Allowed);
        Assert.True(quota.TryConsumeMinute(Beta).Allowed);
        _clock.Advance(TimeSpan.FromSeconds(60));
        Assert.True(quota.TryConsumeMinute(Acme).Allowed);
    }

    [Fact]
    public async Task TryConsumeMinute_UnderConcurrency_NeverExceedsTheLimit()
    {
        SetLocal(new DateOnly(2026, 10, 18), new TimeOnly(10, 0));
        var quota = CreateQuota(perMinute: 7);

        var results = await Task.WhenAll(Enumerable.Range(0, 200).Select(_ => Task.Run(() => quota.TryConsumeMinute(Acme).Allowed)));

        Assert.Equal(7, results.Count(allowed => allowed));
    }

    [Fact]
    public void TryConsumeDaily_AfterTheDayChanges_PurgesTheEntriesOfThePreviousDayInASingleSweep()
    {
        SetLocal(new DateOnly(2026, 10, 18), new TimeOnly(10, 0));
        var quota = CreateQuota();
        foreach (var company in Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()))
        {
            quota.TryConsumeDaily(company, Ana);
        }

        Assert.Equal(5, quota.TrackedCompanies);
        _clock.Advance(TimeSpan.FromHours(24));
        quota.TryConsumeDaily(Acme, Ana);

        Assert.Equal(1, quota.TrackedCompanies);
    }
}
