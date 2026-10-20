using Microsoft.Extensions.AI;

namespace TimeTracker.Infrastructure.Assistant;

/// <summary>Crea el cliente del proveedor de una configuración, o <c>null</c> si el asistente está deshabilitado.</summary>
internal interface IChatClientFactory
{
    IChatClient? Create(AiOptions options);
}

internal sealed class DefaultChatClientFactory : IChatClientFactory
{
    public IChatClient? Create(AiOptions options) => ChatClientFactory.Create(options);
}
