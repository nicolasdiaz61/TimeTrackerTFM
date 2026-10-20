using Microsoft.Extensions.AI;

namespace TimeTracker.Infrastructure.Tests.Fakes;

/// <summary><see cref="IChatClient"/> que responde con un texto fijo y cuenta cuántas veces se libera, para comprobar el ciclo de vida.</summary>
internal sealed class DisposalTrackingChatClient(string reply = "hola") : IChatClient
{
    public int DisposeCount { get; private set; }

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, reply)));

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() => DisposeCount++;
}
