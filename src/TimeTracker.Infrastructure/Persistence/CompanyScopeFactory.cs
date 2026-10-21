using Microsoft.Extensions.DependencyInjection;

namespace TimeTracker.Infrastructure.Persistence;

/// <summary>Crea un ámbito nuevo y le fija la compañía mediante el setter registrado en la raíz de composición.</summary>
public sealed class CompanyScopeFactory(IServiceScopeFactory scopeFactory) : ICompanyScopeFactory
{
    public AsyncServiceScope CreateScope(Guid companyId)
    {
        var scope = scopeFactory.CreateAsyncScope();
        try
        {
            scope.ServiceProvider.GetRequiredService<ICompanyContextSetter>().Set(companyId);
        }
        catch
        {
            scope.Dispose();
            throw;
        }

        return scope;
    }
}
