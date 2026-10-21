using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using TimeTracker.Application.Assistant.Configuration;
using TimeTracker.Application.Companies;
using TimeTracker.Application.Tests.Fakes;
using TimeTracker.Application.Users;
using TimeTracker.Domain.Companies;
using TimeTracker.Domain.Users;
using TimeTracker.Infrastructure.Assistant;
using TimeTracker.Infrastructure.Tests.Fakes;

namespace TimeTracker.Api.IntegrationTests.Assistant;

/// <summary>
/// API con el asistente del entorno en Ollama y las dependencias de red sustituidas: fábrica de clientes y probador de
/// conexión falsos, así que ninguna prueba contacta a un proveedor. Con <paramref name="connectionString"/> usa PostgreSQL real
/// (migrada al arrancar); con <paramref name="store"/> usa ese almacén en memoria y no hay base de datos. El usuario autenticado
/// es <see cref="ManagerId"/> con el rol indicado.
/// </summary>
internal sealed class AssistantConfigurationApiFactory(
    string? connectionString = null,
    string role = "Manager",
    bool masterKeyAvailable = true,
    IAssistantSettingsStore? store = null,
    IUserRepository? users = null) : ApiFactory
{
    public const string EnvironmentModel = "modelo-env";

    // 32 bytes inventados en Base64: solo sirven para estas pruebas.
    public static readonly string MasterKey = Convert.ToBase64String(Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());

    public Guid ManagerId { get; } = Guid.NewGuid();

    public RecordingChatClientFactory ChatFactory { get; } = new();

    public FakeConnectionTester Tester { get; } = new();

    protected override Guid AuthenticatedUserId => ManagerId;

    protected override string AuthenticatedRole => role;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        if (connectionString is not null)
        {
            builder.UseSetting("ConnectionStrings:Default", connectionString);
            builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
        }

        builder.UseSetting("Ai:Provider", "Ollama");
        builder.UseSetting("Ai:Model", EnvironmentModel);
        builder.UseSetting("Ai:Endpoint", "http://localhost:11434/");
        builder.UseSetting("Ai:AllowedProviders", "Anthropic,OpenAI,Ollama");
        if (masterKeyAvailable)
        {
            builder.UseSetting("Ai:MasterKeys:1", MasterKey);
            builder.UseSetting("Ai:ActiveMasterKeyVersion", "1");
        }

        builder.ConfigureTestServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<IChatClientFactory>(ChatFactory));
            services.Replace(ServiceDescriptor.Singleton<IAssistantConnectionTester>(Tester));
            if (users is not null)
            {
                services.Replace(ServiceDescriptor.Scoped(_ => users));
            }

            if (store is not null)
            {
                services.Replace(ServiceDescriptor.Singleton(store));
                // Sin base de datos: la compañía del token (la por defecto) es la de plataforma, como en la demo.
                var companies = new InMemoryCompanyRepository();
                companies.Seed(Company.DefaultId, "Demo", usesPlatformAssistant: true);
                services.Replace(ServiceDescriptor.Scoped<ICompanyRepository>(_ => companies));
            }
        });
    }
}

/// <summary>Probador falso: devuelve el resultado guionizado y registra los candidatos que recibe (nunca hace red).</summary>
internal sealed class FakeConnectionTester : IAssistantConnectionTester
{
    public ConnectionTestResult Result { get; set; } = new("ok", "Conexión correcta.");

    public List<AssistantConnectionCandidate> Candidates { get; } = [];

    public Task<ConnectionTestResult> TestAsync(AssistantConnectionCandidate candidate, CancellationToken cancellationToken)
    {
        Candidates.Add(candidate);
        return Task.FromResult(Result);
    }
}

/// <summary>Almacén en memoria que cuenta las operaciones: demuestra que una petición rechazada no leyó ni escribió nada.</summary>
internal sealed class CountingSettingsStore : IAssistantSettingsStore
{
    public int GetCalls { get; private set; }

    public int WriteCalls { get; private set; }

    public Task<AssistantSettings?> GetAsync(CancellationToken cancellationToken)
    {
        GetCalls++;
        return Task.FromResult<AssistantSettings?>(null);
    }

    public Task<string> GetVersionAsync(CancellationToken cancellationToken)
    {
        GetCalls++;
        return Task.FromResult("0");
    }

    public Task<SaveOutcome> SaveAsync(AssistantSettings next, string expectedToken, AuditEntry audit, CancellationToken cancellationToken)
    {
        WriteCalls++;
        return Task.FromResult<SaveOutcome>(new SaveOutcome.Saved("2"));
    }

    public Task<DeleteOutcome> DeleteAsync(string expectedToken, AuditEntry audit, CancellationToken cancellationToken)
    {
        WriteCalls++;
        return Task.FromResult(DeleteOutcome.NotFound);
    }
}

/// <summary>Almacén cuya fila existe al leer pero cambia antes de borrar: simula a otro Manager guardando entre la lectura y el borrado.</summary>
internal sealed class RacingDeleteStore : IAssistantSettingsStore
{
    private static readonly AssistantSettings Row = new(
        AssistantProvider.Ollama, "modelo-x", null, null, null, null, Guid.NewGuid(), DateTimeOffset.UnixEpoch, "7");

    public Task<AssistantSettings?> GetAsync(CancellationToken cancellationToken) => Task.FromResult<AssistantSettings?>(Row);

    public Task<string> GetVersionAsync(CancellationToken cancellationToken) => Task.FromResult(Row.Version);

    public Task<SaveOutcome> SaveAsync(AssistantSettings next, string expectedToken, AuditEntry audit, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task<DeleteOutcome> DeleteAsync(string expectedToken, AuditEntry audit, CancellationToken cancellationToken)
        => Task.FromResult(DeleteOutcome.Conflict);
}

/// <summary>Repositorio cuya consulta del nombre visible falla: la configuración ya se confirmó y debe seguir respondiendo.</summary>
internal sealed class DisplayNameFailingUserRepository : IUserRepository
{
    public Task<string?> FindDisplayNameAsync(Guid userId, CancellationToken cancellationToken)
        => throw new TimeoutException("consulta del nombre visible fallida");

    public Task<UserAccount?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyList<UserAccount>> ListDirectReportsAsync(Guid managerId, int limit, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<UserAccount?> FindDirectReportAsync(Guid managerId, Guid memberId, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task AddAsync(UserAccount user, CancellationToken cancellationToken) => throw new NotSupportedException();
}
