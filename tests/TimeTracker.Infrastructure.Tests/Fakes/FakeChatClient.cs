using Microsoft.Extensions.AI;

namespace TimeTracker.Infrastructure.Tests.Fakes;

/// <summary>
/// <see cref="IChatClient"/> guionizado: cada llamada consume el siguiente turno del guion y registra los mensajes y
/// las <see cref="ChatOptions"/> recibidos. Nunca llama a un modelo real.
/// </summary>
internal sealed class FakeChatClient(params Func<IReadOnlyList<ChatMessage>, ChatResponse>[] script) : IChatClient
{
    private readonly Queue<Func<IReadOnlyList<ChatMessage>, ChatResponse>> _script = new(script);

    public List<(IReadOnlyList<ChatMessage> Messages, ChatOptions? Options)> Calls { get; } = [];

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        // Copia: el cliente de invocación de funciones sigue ampliando la lista original.
        IReadOnlyList<ChatMessage> snapshot = [.. messages];
        Calls.Add((snapshot, options));

        return _script.TryDequeue(out var turn)
            ? Task.FromResult(turn(snapshot))
            : throw new InvalidOperationException("El guion del FakeChatClient se ha agotado.");
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("El asistente no usa respuestas en streaming.");

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
