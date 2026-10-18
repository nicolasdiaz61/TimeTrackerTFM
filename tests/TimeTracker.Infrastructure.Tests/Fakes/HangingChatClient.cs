using Microsoft.Extensions.AI;

namespace TimeTracker.Infrastructure.Tests.Fakes;

/// <summary>
/// <see cref="IChatClient"/> que no responde hasta que se cancela la llamada: simula un proveedor lento para probar
/// el tiempo máximo y la cancelación del cliente. <see cref="Started"/> avisa de que la llamada ya está en curso.
/// </summary>
internal sealed class HangingChatClient : IChatClient
{
    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Started => _started.Task;

    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        _started.TrySetResult();
        await Task.Delay(Timeout.Infinite, cancellationToken);
        throw new InvalidOperationException("Inalcanzable: la espera infinita solo termina con una cancelación.");
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("El asistente no usa respuestas en streaming.");

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
