using Microsoft.Extensions.AI;

namespace TimeTracker.Infrastructure.Assistant;

/// <summary>
/// Implementación por defecto de <see cref="IChatClientFactory"/>. La costura inyectable existe para que la recarga en
/// caliente (PR 5) cree clientes con valores resueltos en ejecución y para que las pruebas sustituyan el cliente del proveedor.
/// </summary>
internal sealed class DefaultChatClientFactory : IChatClientFactory
{
    public IChatClient? Create(AiOptions options) => ChatClientFactory.Create(options);
}
