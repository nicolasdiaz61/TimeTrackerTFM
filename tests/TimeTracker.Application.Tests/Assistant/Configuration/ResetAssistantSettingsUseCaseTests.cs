using Microsoft.Extensions.Time.Testing;

using TimeTracker.Application.Assistant.Configuration;
using TimeTracker.Application.Tests.Fakes;
using TimeTracker.Domain.Users;

using static TimeTracker.Application.Tests.Assistant.Configuration.ConfigurationTestData;

namespace TimeTracker.Application.Tests.Assistant.Configuration;

public class ResetAssistantSettingsUseCaseTests
{
    private static readonly Guid Ana = Guid.NewGuid();

    private readonly InMemorySettingsStore _store = new();
    private readonly FakeReloader _reloader = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 19, 9, 0, 0, TimeSpan.Zero));

    private Task<DeleteOutcome> Reset()
        => new ResetAssistantSettingsUseCase(_store, _reloader, new FixedCurrentUser(Ana, UserRole.Manager), _time).ExecuteAsync(CancellationToken.None);

    [Fact]
    public async Task WithARow_DeletesItAuditsTheResetAndReloads()
    {
        _store.Row = Stored(model: "db-m");

        Assert.Equal(DeleteOutcome.Deleted, await Reset());

        Assert.Null(_store.Row);
        var audit = Assert.Single(_store.Audits);
        Assert.Equal((AuditAction.Reset, Ana, _time.GetUtcNow()), (audit.Action, audit.ActorId, audit.OccurredAt));
        Assert.Equal("db-m", audit.Previous!.Model);
        Assert.Null(audit.Current);
        Assert.Equal(1, _reloader.Calls);
    }

    [Fact]
    public async Task WithoutARow_NothingIsAuditedOrReloaded()
    {
        Assert.Equal(DeleteOutcome.NotFound, await Reset());

        Assert.Empty(_store.Audits);
        Assert.Equal(0, _reloader.Calls);
    }

    [Fact]
    public async Task IfTheReloadFails_TheResetIsStillConfirmed()
    {
        _store.Row = Stored();
        _reloader.Fails = true;

        Assert.Equal(DeleteOutcome.Deleted, await Reset());

        Assert.Null(_store.Row);
    }

    [Fact]
    public async Task IfAnotherManagerSavesBetweenReadAndDelete_ReportsConflictAndKeepsTheNewRow()
    {
        _store.Row = Stored(model: "old-m");
        _store.AfterRead = store => store.Row = Stored(model: "new-m", version: "11");

        Assert.Equal(DeleteOutcome.Conflict, await Reset());

        Assert.Equal("new-m", _store.Row!.Model);
        Assert.Empty(_store.Audits);
        Assert.Equal(0, _reloader.Calls);
    }

    [Fact]
    public async Task IfAnotherManagerResetsFirst_IsIdempotentWithoutAuditingTwice()
    {
        _store.Row = Stored();
        _store.AfterRead = store => store.Row = null;

        Assert.Equal(DeleteOutcome.NotFound, await Reset());

        Assert.Empty(_store.Audits);
        Assert.Equal(0, _reloader.Calls);
    }
}
