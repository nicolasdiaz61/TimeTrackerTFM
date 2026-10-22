using Microsoft.Extensions.Time.Testing;

using TimeTracker.Infrastructure.Assistant;

namespace TimeTracker.Infrastructure.Tests.Assistant;

/// <summary>
/// La lectura de la instantánea y la expulsión se serializan en la misma sección crítica: quien llega antes de la expulsión recibe la
/// instantánea real (su pipeline vive la gracia) y quien llega después sabe que debe recargar, nunca recibe la deshabilitada de la expulsión.
/// </summary>
public sealed class CompanyRuntimeTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);

    private static CompanyRuntime NewRuntime(FakeTimeProvider time, out AssistantChatClient client)
    {
        client = new AssistantChatClient(null, new AiOptions { Provider = "Ollama", Model = "m" }, time, "7");
        return new CompanyRuntime(Guid.NewGuid(), client, usesPlatform: false, Start);
    }

    [Fact]
    public void TryAcquire_BeforeTheEviction_ReturnsTheCurrentSnapshot_AndRecordsTheUse()
    {
        var time = new FakeTimeProvider(Start);
        var runtime = NewRuntime(time, out var client);
        var later = Start.AddMinutes(3);

        var snapshot = runtime.TryAcquire(later);

        Assert.Same(client.Current, snapshot);
        Assert.Equal(later, runtime.LastUsed);
    }

    [Fact]
    public void TryAcquire_AfterTheEviction_ReturnsNull_AndDoesNotRecordTheUse()
    {
        var time = new FakeTimeProvider(Start);
        var runtime = NewRuntime(time, out _);
        Assert.True(runtime.Evict(new AiOptions()));

        var snapshot = runtime.TryAcquire(Start.AddMinutes(3));

        Assert.Null(snapshot);
        Assert.Equal(Start, runtime.LastUsed);
    }

    [Fact]
    public void TryAdmit_IsGrantedOnlyOnce()
    {
        var runtime = NewRuntime(new FakeTimeProvider(Start), out _);

        Assert.True(runtime.TryAdmit());
        Assert.False(runtime.TryAdmit());
    }

    [Fact]
    public async Task EveryAcquireEitherSeesTheRealSnapshotOrTheEviction_NeverTheDisabledOneOfTheEviction()
    {
        var time = new FakeTimeProvider(Start);
        var runtime = NewRuntime(time, out _);
        var results = new System.Collections.Concurrent.ConcurrentBag<AssistantSnapshot?>();
        using var barrier = new Barrier(9);

        var readers = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (var i = 0; i < 2_000; i++)
            {
                results.Add(runtime.TryAcquire(Start));
            }
        })).ToArray();
        var evictor = Task.Run(() =>
        {
            barrier.SignalAndWait();
            runtime.Evict(new AiOptions());
        });
        await Task.WhenAll([.. readers, evictor]);

        // Un «null» es la señal de recargar; una instantánea devuelta es siempre la real (versión «7»), nunca la deshabilitada («0»).
        Assert.All(results, snapshot => Assert.True(snapshot is null || snapshot.Version == "7"));
    }
}
