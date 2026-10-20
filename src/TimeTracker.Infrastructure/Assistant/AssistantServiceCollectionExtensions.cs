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
    /// deshabilitado. Ciclos de vida: cliente y cuota son singleton (el cliente guarda el pipeline y el contador
    /// diario debe sobrevivir a la petición); el motor y las herramientas dependen de la identidad y del
    /// <c>DbContext</c> del ámbito, así que son de ámbito. Un motor singleton no pasaría la validación del contenedor.
    /// </summary>
    public static IServiceCollection AddAssistant(this IServiceCollection services)
    {
        services.AddSingleton<IValidateOptions<AiOptions>, AiOptionsValidator>();
        services.AddOptions<AiOptions>()
            .BindConfiguration(AiOptions.SectionName)
            .ValidateOnStart();

        services.TryAddSingleton<IChatClientFactory, DefaultChatClientFactory>();
        // El contenedor libera el cliente al cerrar y con él el del proveedor.
        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<AiOptions>>().Value;
            return new AssistantChatClient(provider.GetRequiredService<IChatClientFactory>().Create(options), options, provider.GetRequiredService<TimeProvider>());
        });
        // Singleton: decodifica y valida las claves maestras una sola vez; sin clave activa válida queda no disponible.
        services.AddSingleton<IAssistantCredentialProtector, AesGcmCredentialProtector>();
        // Recarga en caliente (DD-10, DD-11): el cargador es singleton y abre su propio ámbito para el DbContext. El sondeo lo registra Api.
        services.AddSingleton<IAssistantEnvironment, ConfigurationAssistantEnvironment>();
        services.AddSingleton<IAssistantSettingsLoader, AssistantSettingsLoader>();
        services.AddSingleton<IAssistantRuntimeReloader, AssistantRuntimeReloader>();
        // Sin estado: crea y desecha un cliente por prueba y no comparte nada con la instantánea del chat (DD-13).
        services.AddSingleton<IAssistantConnectionTester, AssistantConnectionTester>();
        services.AddSingleton<IAssistantQuota, InMemoryAssistantQuota>();
        services.AddScoped<AssistantToolset>();
        services.AddScoped<IAssistantEngine, ChatAssistantEngine>();
        services.AddScoped<AskAssistantUseCase>();

        return services;
    }
}
