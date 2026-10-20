using Microsoft.Extensions.AI;

namespace TimeTracker.Infrastructure.Assistant;

/// <summary>Cliente de chat que libera, al desecharse, el <see cref="HttpClient"/> que su cliente interno no posee.</summary>
internal sealed class OwnedHttpClientChatClient(IChatClient inner, HttpClient owned) : DelegatingChatClient(inner)
{
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            owned.Dispose();
        }

        base.Dispose(disposing);
    }
}
