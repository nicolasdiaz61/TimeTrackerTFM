using Microsoft.Extensions.Time.Testing;

using TimeTracker.Api.Registration;

namespace TimeTracker.Api.IntegrationTests.Registration;

/// <summary>Tope global de altas por ventana fija: se reserva antes de registrar y se libera si el registro no llega a crearse.</summary>
public class RegistrationGlobalLimiterTests
{
    private static readonly TimeSpan Window = TimeSpan.FromHours(1);
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 22, 8, 0, 0, TimeSpan.Zero));

    private RegistrationGlobalLimiter Create(int limit) => new(_clock, limit, Window);

    [Fact]
    public void ReservesUpToTheLimit_ThenRejectsWithTheTimeLeftInTheWindow()
    {
        var limiter = Create(2);

        Assert.NotNull(limiter.Reserve(out _));
        _clock.Advance(TimeSpan.FromMinutes(10));
        Assert.NotNull(limiter.Reserve(out _));
        Assert.Null(limiter.Reserve(out var retryAfter));
        Assert.Equal(TimeSpan.FromMinutes(50), retryAfter);
    }

    [Fact]
    public void AReleasedReservation_FreesASlot()
    {
        var limiter = Create(1);
        var reservation = limiter.Reserve(out _);
        Assert.NotNull(reservation);
        limiter.Release(reservation.Value);

        Assert.NotNull(limiter.Reserve(out _));
        Assert.Null(limiter.Reserve(out _));
    }

    [Fact]
    public void WhenTheWindowEnds_TheCountStartsAgain()
    {
        var limiter = Create(1);
        Assert.NotNull(limiter.Reserve(out _));
        Assert.Null(limiter.Reserve(out _));

        _clock.Advance(Window + TimeSpan.FromSeconds(1));

        Assert.NotNull(limiter.Reserve(out _));
    }

    [Fact]
    public void AReleaseFromAnEarlierWindow_DoesNotFreeASlotOfTheCurrentOne()
    {
        var limiter = Create(1);
        var old = limiter.Reserve(out _);
        _clock.Advance(Window + TimeSpan.FromSeconds(1));
        Assert.NotNull(limiter.Reserve(out _));

        limiter.Release(old!.Value);

        Assert.Null(limiter.Reserve(out _));
    }
}
