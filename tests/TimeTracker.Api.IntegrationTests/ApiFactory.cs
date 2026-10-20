using System.Net.Http.Headers;
using System.Runtime.ExceptionServices;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

using TimeTracker.Infrastructure.Assistant;

namespace TimeTracker.Api.IntegrationTests;

/// <summary>
/// Fábrica con una configuración base válida (origen, cadena de conexión sin contraseña a un servidor
/// inexistente, clave de firma inventada y reloj ficticio) para las pruebas que no se ocupan del arranque.
/// El cliente por defecto va autenticado como <see cref="DefaultUserId"/> (empleado); <see cref="CreateAnonymousClient"/>
/// no lleva cabecera. Para cambiar o quitar una clave: <c>WithWebHostBuilder(builder =&gt; builder.UseSetting(clave, valor_o_null))</c>.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public const string ValidOrigin = "https://app.example.test";
    public const string ValidConnectionString = "Host=db.invalid;Database=timetracker;Username=timetracker";

    // Clave inventada de 64 bytes (suficiente también para HS512, para que el rechazo de ese algoritmo no sea por tamaño de clave).
    public const string ValidSigningKey = "clave-de-firma-de-prueba-0123456789012345678901234567890123456789";

    public static readonly Guid DefaultUserId = Guid.Parse("5b1f0c1e-7a44-4f0e-9c52-3d8a6e2b9f10");

    public static readonly DateTimeOffset DefaultNow = new(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);

    static ApiFactory() => EnvironmentIsolation.Apply();

    /// <summary>Reloj de la aplicación: emisión y validación del token y fichajes usan este instante controlable (D7).</summary>
    public FakeTimeProvider Clock { get; } = new(DefaultNow);

    protected virtual Guid AuthenticatedUserId => DefaultUserId;

    protected virtual string AuthenticatedRole => "Employee";

    /// <summary>
    /// El sondeo de la configuración guardada se elimina por defecto: la base de datos de estas pruebas no existe y no debe haber
    /// conexiones en segundo plano. Solo las fábricas con PostgreSQL real que lo ejercitan lo conservan (DD-11).
    /// </summary>
    protected virtual bool KeepsAssistantSettingsPoller => false;

    public HttpClient CreateClientFor(Guid userId, string role)
    {
        var client = CreateDefaultClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenFor(userId, role));
        return client;
    }

    public string TokenFor(Guid userId, string role) => TestTokens.Create(userId, role, Clock.GetUtcNow());

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Sin vigilancia de appsettings*.json: en Linux cada host vigilante gasta una instancia de inotify y el límite
        // por usuario se agota con muchas fábricas vivas (el arranque falla con IOException en vez de validar opciones).
        builder.UseSetting("hostBuilder:reloadConfigOnChange", "false");
        builder.UseSetting("Cors:AllowedOrigins:0", ValidOrigin);
        builder.UseSetting("ConnectionStrings:Default", ValidConnectionString);
        builder.UseSetting("Jwt:SigningKey", ValidSigningKey);
        builder.ConfigureTestServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<TimeProvider>(Clock));
            if (!KeepsAssistantSettingsPoller)
            {
                foreach (var poller in services.Where(descriptor => descriptor.ImplementationType == typeof(AssistantSettingsPoller)).ToList())
                {
                    services.Remove(poller);
                }
            }
        });
    }

    /// <summary>Gancho entre la construcción del host y su arranque; solo lo usan las pruebas de la carrera de arranque.</summary>
    protected virtual void OnHostBuilt(IHost host)
    {
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = builder.Build();
        OnHostBuilt(host);
        try
        {
            host.Start();
        }
        catch (ObjectDisposedException disposed) when (disposed.ObjectName == "IServiceProvider"
            && DeferredHostFailure.TryGet(host, out var realFailure))
        {
            // Carrera del framework: el hilo de la aplicación falló y destruyó el host antes de que DeferredHost.StartAsync
            // lo tocara. El error real sí se guardó (EntryPointCompleted): se entrega en lugar del ObjectDisposedException.
            ExceptionDispatchInfo.Capture(realFailure).Throw();
        }

        return host;
    }

    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenFor(AuthenticatedUserId, AuthenticatedRole));
    }
}
