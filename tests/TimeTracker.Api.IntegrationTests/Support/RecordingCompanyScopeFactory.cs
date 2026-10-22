using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

using TimeTracker.Infrastructure.Persistence;

namespace TimeTracker.Api.IntegrationTests.Support;

/// <summary>Decora la fábrica de ámbitos real y anota las compañías con las que se pidió un ámbito.</summary>
internal sealed class RecordingCompanyScopeFactory : ICompanyScopeFactory
{
    private readonly List<Guid> _requested = [];
    private ICompanyScopeFactory? _inner;

    public IReadOnlyList<Guid> Requested
    {
        get
        {
            lock (_requested)
            {
                return [.. _requested];
            }
        }
    }

    public AsyncServiceScope CreateScope(Guid companyId)
    {
        lock (_requested)
        {
            _requested.Add(companyId);
        }

        return _inner!.CreateScope(companyId);
    }

    /// <summary>Sustituye la fábrica registrada por esta, que envuelve a la real.</summary>
    public void InstallOn(IWebHostBuilder builder)
        => builder.ConfigureServices(services =>
        {
            var real = services.Single(descriptor => descriptor.ServiceType == typeof(ICompanyScopeFactory));
            services.Remove(real);
            services.AddSingleton<ICompanyScopeFactory>(provider =>
            {
                _inner = (ICompanyScopeFactory)ActivatorUtilities.CreateInstance(provider, real.ImplementationType!);
                return this;
            });
        });
}
