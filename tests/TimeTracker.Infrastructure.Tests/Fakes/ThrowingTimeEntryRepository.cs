using TimeTracker.Application.TimeTracking;
using TimeTracker.Domain.TimeTracking;

namespace TimeTracker.Infrastructure.Tests.Fakes;

/// <summary>Repositorio de fichajes que siempre falla con un texto reconocible, para comprobar que no llega al modelo ni al registro.</summary>
internal sealed class ThrowingTimeEntryRepository : ITimeEntryRepository
{
    private static InvalidOperationException Failure() => new("clave-secreta-prueba");

    public Task<TimeEntry?> FindOpenAsync(Guid employeeId, CancellationToken cancellationToken) => throw Failure();

    public Task<IReadOnlyList<TimeEntry>> ListLatestAsync(Guid employeeId, int limit, CancellationToken cancellationToken) => throw Failure();

    public Task<IReadOnlyList<TimeEntry>> ListInWindowAsync(Guid employeeId, TimeWindow window, int limit, CancellationToken cancellationToken) => throw Failure();

    public Task AddAsync(TimeEntry entry, CancellationToken cancellationToken) => throw Failure();

    public Task UpdateAsync(TimeEntry entry, CancellationToken cancellationToken) => throw Failure();

    public Task ReplaceOpenAsync(TimeEntry closed, TimeEntry next, CancellationToken cancellationToken) => throw Failure();
}
