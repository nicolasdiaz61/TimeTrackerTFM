using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using TimeTracker.Infrastructure.Assistant;

namespace TimeTracker.Api.IntegrationTests.Assistant;

/// <summary>
/// API con el asistente habilitado sobre un <see cref="IChatClient"/> falso: el <c>AssistantChatClient</c> real
/// (con su pipeline de herramientas) se monta sobre el falso, así que se ejercita todo menos el proveedor.
/// Sin modelo, la API queda como una compañía sin asistente. Sin base de datos: el nombre de host no existe, así que el registro de
/// instantáneas se sustituye por uno fijo (<see cref="FixedRuntimeRegistry"/>): <paramref name="model"/> sirve a toda compañía,
/// salvo que <paramref name="modelsByCompany"/> asigne uno distinto (o ninguno) a una compañía concreta.
/// </summary>
public sealed class AssistantApiFactory(
    IChatClient? model = null,
    IReadOnlyDictionary<string, string?>? settings = null,
    Guid? userId = null,
    string role = "Employee",
    IReadOnlyDictionary<Guid, IChatClient?>? modelsByCompany = null) : ApiFactory
{
    protected override Guid AuthenticatedUserId => userId ?? DefaultUserId;

    protected override string AuthenticatedRole => role;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        if (model is not null)
        {
            builder.UseSetting("Ai:Provider", "Ollama");
            builder.UseSetting("Ai:Model", "modelo-ficticio");
            builder.UseSetting("Ai:Endpoint", "http://localhost:11434/");
        }

        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAssistantRuntimeRegistry>();
            services.AddSingleton<IAssistantRuntimeRegistry>(provider
                => new FixedRuntimeRegistry(model, modelsByCompany, provider.GetRequiredService<IOptions<AiOptions>>().Value));
        });
    }
}

/// <summary>Registro de instantáneas sin base de datos: una instantánea fija por compañía, construida sobre un cliente falso.</summary>
internal sealed class FixedRuntimeRegistry : IAssistantRuntimeRegistry, IDisposable
{
    private readonly Dictionary<Guid, AssistantChatClient> _clients = [];
    private readonly AssistantChatClient _default;

    public FixedRuntimeRegistry(IChatClient? model, IReadOnlyDictionary<Guid, IChatClient?>? byCompany, AiOptions options)
    {
        _default = new AssistantChatClient(model, options);
        foreach (var (company, client) in byCompany ?? new Dictionary<Guid, IChatClient?>())
        {
            _clients[company] = new AssistantChatClient(client, options);
        }
    }

    public ValueTask<AssistantSnapshot> GetAsync(Guid companyId, CancellationToken cancellationToken)
        => ValueTask.FromResult((_clients.GetValueOrDefault(companyId) ?? _default).Current);

    public Task ReloadAsync(Guid companyId, CancellationToken cancellationToken) => Task.CompletedTask;

    public IReadOnlyList<LoadedRuntime> Loaded() => [];

    public int ExpireIdle() => 0;

    public void Dispose()
    {
        _default.Dispose();
        foreach (var client in _clients.Values)
        {
            client.Dispose();
        }
    }
}
