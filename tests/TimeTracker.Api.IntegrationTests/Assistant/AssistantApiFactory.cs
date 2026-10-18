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
/// Sin modelo, la API queda como en producción sin <c>Ai:Provider</c>. Sin base de datos: el nombre de host no existe.
/// </summary>
public sealed class AssistantApiFactory(IChatClient? model = null, IReadOnlyDictionary<string, string?>? settings = null, Guid? userId = null, string role = "Employee") : ApiFactory
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

        if (model is not null)
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<AssistantChatClient>();
                services.AddSingleton(provider => new AssistantChatClient(model, provider.GetRequiredService<IOptions<AiOptions>>().Value));
            });
        }
    }
}
