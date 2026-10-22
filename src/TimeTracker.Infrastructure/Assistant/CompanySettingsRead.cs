using TimeTracker.Application.Assistant.Configuration;

namespace TimeTracker.Infrastructure.Assistant;

/// <summary>Lo que se lee de una compañía para construir su instantánea: su fila (si la hay) y si usa los valores de plataforma.</summary>
internal sealed record CompanySettingsRead(AssistantSettings? Stored, bool UsesPlatform);

/// <summary>Lee la configuración de una compañía fuera de una petición HTTP (registro de instantáneas); un fallo de la base de datos se propaga.</summary>
internal interface ICompanySettingsReader
{
    Task<CompanySettingsRead> ReadAsync(Guid companyId, CancellationToken cancellationToken);
}
