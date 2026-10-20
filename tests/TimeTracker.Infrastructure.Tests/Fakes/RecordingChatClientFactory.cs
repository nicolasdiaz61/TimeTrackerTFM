using Microsoft.Extensions.AI;

using TimeTracker.Infrastructure.Assistant;

namespace TimeTracker.Infrastructure.Tests.Fakes;

/// <summary>Fábrica falsa: registra las opciones recibidas y devuelve un cliente de seguimiento (o <c>null</c> sin proveedor). Nunca hace red.</summary>
internal sealed class RecordingChatClientFactory : IChatClientFactory
{
    public List<AiOptions> Created { get; } = [];

    public List<DisposalTrackingChatClient> Clients { get; } = [];

    public Exception? Failure { get; set; }

    public IChatClient? Create(AiOptions options)
    {
        Created.Add(options);
        if (Failure is { } failure)
        {
            throw failure;
        }

        if (!options.IsEnabled)
        {
            return null;
        }

        var client = new DisposalTrackingChatClient($"respuesta de {options.Model}");
        Clients.Add(client);
        return client;
    }
}
