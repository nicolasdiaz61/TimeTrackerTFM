using Microsoft.Extensions.AI;

namespace TimeTracker.Infrastructure.Assistant;

/// <summary>
/// Cliente de chat del asistente ya montado con su pipeline (DD-13), o sin cliente si no hay proveedor configurado.
/// Es singleton y desechable: el contenedor lo libera al cerrar y con él el cliente del proveedor.
/// </summary>
public sealed class AssistantChatClient : IDisposable
{
    private readonly IChatClient? _pipeline;

    /// <param name="inner">Cliente del proveedor, o <c>null</c> si el asistente está deshabilitado.</param>
    public AssistantChatClient(IChatClient? inner, AiOptions options)
        => _pipeline = inner is null ? null : AssistantChatPipeline.Build(inner, options);

    public bool IsEnabled => _pipeline is not null;

    internal IChatClient? Pipeline => _pipeline;

    public void Dispose() => _pipeline?.Dispose();
}
