using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using TimeTracker.Application.Assistant;

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

        // El contenedor libera el cliente al cerrar y con él el del proveedor.
        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<AiOptions>>().Value;
            return new AssistantChatClient(ChatClientFactory.Create(options), options);
        });
        services.AddSingleton<IAssistantQuota, InMemoryAssistantQuota>();
        services.AddScoped<AssistantToolset>();
        services.AddScoped<IAssistantEngine, ChatAssistantEngine>();
        services.AddScoped<AskAssistantUseCase>();

        return services;
    }
}
