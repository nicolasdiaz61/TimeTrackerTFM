using System.Net;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;

using static TimeTracker.Api.IntegrationTests.Assistant.AssistantClient;
using static TimeTracker.Api.IntegrationTests.Assistant.AssistantConfigurationHttp;
using static TimeTracker.Api.IntegrationTests.Support.TwoCompanyFixture;

namespace TimeTracker.Api.IntegrationTests.Assistant;

/// <summary>
/// TC-10 de punta a punta (DD-14, DD-15): dos compañías A y B sobre PostgreSQL real con el registro de instantáneas y el sondeo global
/// reales, el proveedor sustituido por una fábrica falsa («respuesta de {modelo}») y <c>Ai__SettingsPollSeconds=1</c> con reloj falso.
/// Ninguna prueba espera por tiempo real: el sondeo avanza con una señal explícita.
/// </summary>
[Collection("Postgres")]
public sealed class AssistantRuntimeIsolationTests(PostgresFixture postgres)
{
    private const string OwnKey = "clave-prueba-123456";
    private const string PlatformKey = "clave-plataforma-123456";

    private static readonly string MasterKey = Convert.ToBase64String(Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());

    private static readonly Dictionary<string, string?> PlatformAnthropic = new()
    {
        ["Ai:Provider"] = "Anthropic",
        ["Ai:Model"] = "modelo-plataforma",
        ["Ai:ApiKey"] = PlatformKey,
    };

    private static async Task<string?> ReplyAsync(HttpClient chat)
    {
        var response = await chat.AskAsync("hola");
        if (response.StatusCode != HttpStatusCode.OK)
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            return null;
        }

        using var body = await ReadAsync(response);
        return body.RootElement.GetProperty("reply").GetString();
    }

    private static async Task<string> SaveAsync(HttpClient manager, string json, string expectedToken = "0")
    {
        var response = await manager.PutJsonAsync(json.Replace("\"concurrencyToken\":\"0\"", $"\"concurrencyToken\":\"{expectedToken}\"", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ReadJsonAsync(response);
        return body.RootElement.GetProperty("concurrencyToken").GetString()!;
    }

    private static HttpClient Employee(ReloadApiFactory factory, CompanyMembers company) => TwoCompanyFixture.ClientFor(factory, company, company.Employee1);

    private static HttpClient Manager(ReloadApiFactory factory, CompanyMembers company) => TwoCompanyFixture.ClientFor(factory, company, company.Manager);

    [Fact]
    public async Task EnablingA_DoesNotEnableB_AndEachCompanyUsesItsOwnModel()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        using var factory = new ReloadApiFactory(fixture.ConnectionString, masterKey: MasterKey);
        await SaveAsync(Manager(factory, fixture.A), """{"provider":"Ollama","model":"modelo-de-a","concurrencyToken":"0"}""");

        Assert.Equal("respuesta de modelo-de-a", await ReplyAsync(Employee(factory, fixture.A)));
        Assert.Null(await ReplyAsync(Employee(factory, fixture.B)));

        await SaveAsync(Manager(factory, fixture.B), """{"provider":"Ollama","model":"modelo-de-b","concurrencyToken":"0"}""");
        Assert.Equal("respuesta de modelo-de-b", await ReplyAsync(Employee(factory, fixture.B)));
        Assert.Equal("respuesta de modelo-de-a", await ReplyAsync(Employee(factory, fixture.A)));
    }

    [Fact]
    public async Task ANewCompany_SeesTheAssistantDisabled_EvenWithAnEnvironmentProviderAndPlatformKey_AndThePlatformKeyIsNeverUsedForIt()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        using var factory = new ReloadApiFactory(fixture.ConnectionString, masterKey: MasterKey, settings: PlatformAnthropic);
        var demo = factory.CreateClientFor(Guid.NewGuid(), "Employee");

        Assert.Null(await ReplyAsync(Employee(factory, fixture.B)));
        Assert.Equal("respuesta de modelo-plataforma", await ReplyAsync(demo));

        // Solo la demo construyó un cliente con la clave de plataforma; B no construyó ninguno.
        var created = Assert.Single(factory.ChatFactory.Created);
        Assert.Equal((PlatformKey, "modelo-plataforma"), (created.ApiKey, created.Model));
    }

    [Fact]
    public async Task ACompanyWithItsOwnKey_BuildsItsClientWithThatKey_NeverThePlatformOne()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        using var factory = new ReloadApiFactory(fixture.ConnectionString, masterKey: MasterKey, settings: PlatformAnthropic);
        await SaveAsync(
            Manager(factory, fixture.A),
            $$"""{"provider":"OpenAI","model":"modelo-de-a","concurrencyToken":"0","credential":"{{OwnKey}}"}""");

        Assert.Equal("respuesta de modelo-de-a", await ReplyAsync(Employee(factory, fixture.A)));

        // La demo precarga su instantánea de plataforma al arrancar; la de A es la única de OpenAI y lleva su propia clave.
        var created = Assert.Single(factory.ChatFactory.Created, options => options.Provider == "OpenAI");
        Assert.Equal(OwnKey, created.ApiKey);
        Assert.DoesNotContain(factory.ChatFactory.Created, options => options.Provider != "Anthropic" && options.ApiKey == PlatformKey);
    }

    [Fact]
    public async Task AnExternalSaveOfA_ReachesAThroughThePoll_AndBIsNotRebuilt()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        using var factory = new ReloadApiFactory(fixture.ConnectionString, masterKey: MasterKey);
        await SaveAsync(Manager(factory, fixture.A), """{"provider":"Ollama","model":"a-uno","concurrencyToken":"0"}""");
        await SaveAsync(Manager(factory, fixture.B), """{"provider":"Ollama","model":"b-uno","concurrencyToken":"0"}""");
        Assert.Equal("respuesta de a-uno", await ReplyAsync(Employee(factory, fixture.A)));
        Assert.Equal("respuesta de b-uno", await ReplyAsync(Employee(factory, fixture.B)));
        var buildsOfB = factory.ChatFactory.Created.Count(options => options.Model == "b-uno");

        await ReloadApiFactory.ExecuteAsync(fixture.ConnectionString, $"UPDATE assistant_settings SET model = 'a-dos' WHERE company_id = '{fixture.A.Id}'");
        Assert.Equal("respuesta de a-uno", await ReplyAsync(Employee(factory, fixture.A)));
        await factory.PollOnceAsync();

        Assert.Equal("respuesta de a-dos", await ReplyAsync(Employee(factory, fixture.A)));
        Assert.Equal("respuesta de b-uno", await ReplyAsync(Employee(factory, fixture.B)));
        Assert.Equal(buildsOfB, factory.ChatFactory.Created.Count(options => options.Model == "b-uno"));
    }

    [Fact]
    public async Task TwoInstancesOnTheSameDatabase_SynchronizeThroughTheGlobalPoll_ForTheCompanyThatSaved()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        using var first = new ReloadApiFactory(fixture.ConnectionString, masterKey: MasterKey);
        using var second = new ReloadApiFactory(fixture.ConnectionString, masterKey: MasterKey);
        var token = await SaveAsync(Manager(first, fixture.B), """{"provider":"Ollama","model":"b-uno","concurrencyToken":"0"}""");
        Assert.Equal("respuesta de b-uno", await ReplyAsync(Employee(second, fixture.B)));

        await SaveAsync(Manager(first, fixture.B), """{"provider":"Ollama","model":"b-dos","concurrencyToken":"0"}""", token);

        Assert.Equal("respuesta de b-dos", await ReplyAsync(Employee(first, fixture.B)));
        Assert.Equal("respuesta de b-uno", await ReplyAsync(Employee(second, fixture.B)));
        await second.PollOnceAsync();
        Assert.Equal("respuesta de b-dos", await ReplyAsync(Employee(second, fixture.B)));
    }

    [Fact]
    public async Task ADeletedRow_RestoresTheDisabledStateOfACompanyThatIsNotPlatform_OnTheLocalInstance()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        using var factory = new ReloadApiFactory(fixture.ConnectionString, masterKey: MasterKey, settings: PlatformAnthropic);
        var manager = Manager(factory, fixture.A);
        await SaveAsync(manager, """{"provider":"Ollama","model":"a-uno","concurrencyToken":"0"}""");
        Assert.Equal("respuesta de a-uno", await ReplyAsync(Employee(factory, fixture.A)));

        Assert.Equal(HttpStatusCode.NoContent, (await manager.DeleteAsync(AssistantConfigurationHttp.Route, CancellationToken.None)).StatusCode);

        Assert.Null(await ReplyAsync(Employee(factory, fixture.A)));
    }

    // Aviso de la revisión de la PR 4a: un cambio del indicador debe recargar y degradar a deshabilitado, sin seguir sirviendo la clave de plataforma.
    [Fact]
    public async Task WhenTheDemoLosesThePlatformIndicator_ThePollDisablesItInsteadOfKeepingThePlatformKey()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        using var factory = new ReloadApiFactory(fixture.ConnectionString, masterKey: MasterKey, settings: PlatformAnthropic);
        var demo = factory.CreateClientFor(Guid.NewGuid(), "Employee");
        Assert.Equal("respuesta de modelo-plataforma", await ReplyAsync(demo));

        await ReloadApiFactory.ExecuteAsync(fixture.ConnectionString, "UPDATE companies SET uses_platform_assistant = false WHERE id = '00000000-0000-7000-8000-000000000001'");
        await factory.PollOnceAsync();

        Assert.Null(await ReplyAsync(demo));
    }
}
