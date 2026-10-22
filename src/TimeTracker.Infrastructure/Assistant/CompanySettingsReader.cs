using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using TimeTracker.Application.Assistant.Configuration;
using TimeTracker.Application.Companies;
using TimeTracker.Domain.Companies;
using TimeTracker.Infrastructure.Persistence;

namespace TimeTracker.Infrastructure.Assistant;

/// <summary>
/// Lee la fila y el indicador de plataforma de una compañía dentro de <c>CreateScope(companyId)</c> (DD-7, DD-14): sin petición HTTP no hay
/// compañía en el contexto, así que el ámbito la fija y el filtro global acota la lectura sin <c>IgnoreQueryFilters</c>.
/// </summary>
internal sealed class CompanySettingsReader(ICompanyScopeFactory companyScopes) : ICompanySettingsReader
{
    public async Task<CompanySettingsRead> ReadAsync(Guid companyId, CancellationToken cancellationToken)
    {
        await using var scope = companyScopes.CreateScope(companyId);
        var stored = await scope.ServiceProvider.GetRequiredService<IAssistantSettingsStore>().GetAsync(cancellationToken);
        return new CompanySettingsRead(stored, await ReadUsesPlatformAsync(scope.ServiceProvider, companyId, cancellationToken));
    }

    // Solo una compañía con uses_platform_assistant usa el entorno (proveedor, modelo ni clave). Con la tabla `companies` aún sin crear
    // (la precarga corre antes que las migraciones en una base vacía) manda el comportamiento de siempre: la demo usa el entorno.
    private static async Task<bool> ReadUsesPlatformAsync(IServiceProvider services, Guid companyId, CancellationToken cancellationToken)
    {
        try
        {
            var info = await services.GetRequiredService<ICompanyRepository>().FindLoginInfoAsync(companyId, cancellationToken);
            return info?.UsesPlatformAssistant ?? false;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            return companyId == Company.DefaultId;
        }
    }
}
