using TimeTracker.Application.Assistant.Configuration;
using TimeTracker.Infrastructure.Assistant;

namespace TimeTracker.Infrastructure.Tests.Fakes;

/// <summary>Lector de la configuración por compañía en memoria: permite simular filas, el indicador de plataforma, fallos y esperas.</summary>
internal sealed class FakeCompanySettingsReader : ICompanySettingsReader
{
    private readonly Dictionary<Guid, CompanySettingsRead> _reads = [];
    private int _inFlight;

    public Dictionary<Guid, int> Reads { get; } = [];

    public int MaxConcurrentReads { get; private set; }

    public Exception? Failure { get; set; }

    /// <summary>Si se asigna, la lectura espera a que se complete (o se cancele) antes de responder.</summary>
    public TaskCompletionSource? Gate { get; set; }

    public int TotalReads => Reads.Values.Sum();

    public void Set(Guid companyId, AssistantSettings? stored, bool usesPlatform)
        => _reads[companyId] = new CompanySettingsRead(stored, usesPlatform);

    public async Task<CompanySettingsRead> ReadAsync(Guid companyId, CancellationToken cancellationToken)
    {
        lock (Reads)
        {
            Reads[companyId] = Reads.GetValueOrDefault(companyId) + 1;
        }

        MaxConcurrentReads = Math.Max(MaxConcurrentReads, Interlocked.Increment(ref _inFlight));
        try
        {
            if (Gate is { } gate)
            {
                await gate.Task.WaitAsync(cancellationToken);
            }

            if (Interlocked.Exchange(ref _failureOnce, null) is { } once)
            {
                throw once;
            }

            return Failure is { } failure ? throw failure : _reads.GetValueOrDefault(companyId, new CompanySettingsRead(null, false));
        }
        finally
        {
            Interlocked.Decrement(ref _inFlight);
        }
    }

    private Exception? _failureOnce;

    public void FailNextRead(Exception failure) => Volatile.Write(ref _failureOnce, failure);
}
