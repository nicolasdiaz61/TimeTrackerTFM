using Microsoft.Extensions.Time.Testing;

using TimeTracker.Api.Registration;

namespace TimeTracker.Api.IntegrationTests.Registration;

/// <summary>Acota el número de particiones del límite por IP: pasado el tope, las claves nuevas comparten una partición de desbordamiento.</summary>
public class RegistrationPartitionTrackerTests
{
    private static readonly TimeSpan Window = TimeSpan.FromHours(1);
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 22, 8, 0, 0, TimeSpan.Zero));

    private RegistrationPartitionTracker Create(int max) => new(_clock, max, Window);

    [Fact]
    public void KnownKeys_AreReturnedAsTheyAre_UpToTheMaximum()
    {
        var tracker = Create(3);

        Assert.Equal("a", tracker.Resolve("a"));
        Assert.Equal("b", tracker.Resolve("b"));
        Assert.Equal("c", tracker.Resolve("c"));
        Assert.Equal("a", tracker.Resolve("a"));
        Assert.Equal(3, tracker.Count);
    }

    [Fact]
    public void BeyondTheMaximum_NewKeysShareTheOverflowPartition_AndTheCountDoesNotGrow()
    {
        var tracker = Create(2);
        tracker.Resolve("a");
        tracker.Resolve("b");

        Assert.Equal(RegistrationPartitionTracker.Overflow, tracker.Resolve("c"));
        Assert.Equal(RegistrationPartitionTracker.Overflow, tracker.Resolve("d"));
        Assert.Equal(2, tracker.Count);
        Assert.Equal("a", tracker.Resolve("a"));
    }

    [Fact]
    public void AfterTheWindow_ExpiredKeysAreReleased_AndNewKeysAreTrackedAgain()
    {
        var tracker = Create(2);
        tracker.Resolve("a");
        tracker.Resolve("b");
        Assert.Equal(RegistrationPartitionTracker.Overflow, tracker.Resolve("c"));

        _clock.Advance(Window + TimeSpan.FromSeconds(1));

        Assert.Equal("c", tracker.Resolve("c"));
        Assert.True(tracker.Count <= 2);
    }

    [Fact]
    public void AKeyKeepsItsPartition_UntilItsWindowEnds()
    {
        var tracker = Create(1);
        tracker.Resolve("a");

        _clock.Advance(Window - TimeSpan.FromSeconds(1));

        Assert.Equal("a", tracker.Resolve("a"));
        Assert.Equal(RegistrationPartitionTracker.Overflow, tracker.Resolve("b"));
    }
}
