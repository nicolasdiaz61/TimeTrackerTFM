using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using TimeTracker.Application.Assistant;
using TimeTracker.Application.Assistant.Configuration;

namespace TimeTracker.Infrastructure.Assistant;

public static class AssistantServiceCollectionExtensions
{
    /// <summary>
    /// Registra el asistente (DD-13). Sin <c>Ai:Provider</c> el contenedor se construye igual y el motor queda
    /// deshabilitado. Ciclos de vida: registro de instantáneas y cuota son singleton (el registro guarda los pipelines y el contador
    /// diario debe sobrevivir a la petición); el motor y las herramientas dependen de la identidad y del
    /// <c>DbContext</c> del ámbito, así que son de ámbito. Un motor singleton no pasaría la validación del contenedor.
    /// </summary>
    public static IServiceCollection AddAssistant(this IServiceCollection services)
    {
        services.AddSingleton<IValidateOptions<AiOptions>, AiOptionsValidator>();
        services.AddOptions<AiOptions>()
            .Configure<IConfiguration>(AiOptionsBinder.Bind)
            .ValidateOnStart();

        services.TryAddSingleton<IChatClientFactory, DefaultChatClientFactory>();
        // Singleton: decodifica y valida las claves maestras una sola vez; sin clave activa válida queda no disponible.
        services.AddSingleton<IAssistantCredentialProtector, AesGcmCredentialProtector>();
        // Instantáneas por compañía (DD-14): el registro es singleton y desechable (el contenedor lo libera al cerrar y con él los clientes
        // del proveedor); el lector abre un ámbito con la compañía para leer su fila. El sondeo global lo registra Api.
        services.AddSingleton<IAssistantEnvironment, ConfigurationAssistantEnvironment>();
        services.AddSingleton<AssistantOptionsResolver>();
        services.AddSingleton<ICompanySettingsReader, CompanySettingsReader>();
        services.AddSingleton<IAssistantRuntimeRegistry, AssistantRuntimeRegistry>();
        services.AddSingleton<IAssistantRuntimeReloader, AssistantRuntimeReloader>();
        // Sin estado: crea y desecha un cliente por prueba y no comparte nada con la instantánea del chat (DD-13).
        services.AddSingleton<IAssistantConnectionTester, AssistantConnectionTester>();
        services.AddSingleton<IAssistantQuota, InMemoryAssistantQuota>();
        services.AddScoped<AssistantToolset>();
        services.AddScoped<IAssistantEngine, ChatAssistantEngine>();
        services.AddScoped<AskAssistantUseCase>();

        return services;
    }

    /// <summary>
    /// Sondeo global de la configuración guardada (DD-15). Va después de la migración (<c>DatabaseMigrationHostedService</c> corre en
    /// <c>StartingAsync</c>) y antes de que Kestrel acepte peticiones. La exportación del contrato OpenAPI nunca debe tocar la base de datos.
    /// </summary>
    public static IServiceCollection AddAssistantSettingsPoller(this IServiceCollection services)
        => services.AddHostedService<AssistantSettingsPoller>();
}
