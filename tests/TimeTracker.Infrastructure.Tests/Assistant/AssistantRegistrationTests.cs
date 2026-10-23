using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using TimeTracker.Application.Assistant;
using TimeTracker.Application.Assistant.Configuration;
using TimeTracker.Application.Identity;
using TimeTracker.Application.Team;
using TimeTracker.Application.Tests.Fakes;
using TimeTracker.Application.TimeTracking;
using TimeTracker.Application.Users;
using TimeTracker.Application.WorkSchedules;
using TimeTracker.Domain.Companies;
using TimeTracker.Domain.Users;
using TimeTracker.Infrastructure.Assistant;
using TimeTracker.Infrastructure.Persistence;
using TimeTracker.Infrastructure.Tests.Fakes;

namespace TimeTracker.Infrastructure.Tests.Assistant;

/// <summary>Composición real del asistente con un contenedor que valida ámbitos y dependencias; sin red ni modelo.</summary>
public sealed class AssistantRegistrationTests
{
    private static ServiceProvider Build(params (string Key, string? Value)[] settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(setting => new KeyValuePair<string, string?>(setting.Key, setting.Value)))
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<TimeProvider>(new FakeTimeProvider());
        services.AddLogging();
        services.AddScoped<ICurrentUser>(_ => new FixedCurrentUser(Guid.NewGuid(), UserRole.Employee));
        services.AddScoped<ITimeEntryRepository, InMemoryTimeEntryRepository>();
        services.AddScoped<IUserRepository, InMemoryUserRepository>();
        services.AddScoped<IWorkScheduleRepository>(provider => new InMemoryWorkScheduleRepository((InMemoryUserRepository)provider.GetRequiredService<IUserRepository>()));
        services.AddScoped<GetTimeEntriesUseCase>();
        services.AddScoped<GetTeamMembersUseCase>();
        services.AddScoped<GetTeamMemberTimeEntriesUseCase>();
        services.AddScoped<GetTeamSummaryUseCase>();
        services.AddScoped<RecordingCompanyContext>();
        services.AddScoped<ICompanyContextSetter>(scope => scope.GetRequiredService<RecordingCompanyContext>());
        services.AddSingleton<ICompanyScopeFactory, CompanyScopeFactory>();
        services.AddAssistant();
        // Sin base de datos: la compañía por defecto es la de plataforma (usa el entorno), como la demo.
        var reader = new FakeCompanySettingsReader();
        reader.Set(Company.DefaultId, stored: null, usesPlatform: true);
        services.AddSingleton<ICompanySettingsReader>(reader);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    [Fact]
    public async Task TheConnectionTester_IsRegisteredAndDoesNotNeedAProvider()
    {
        await using var provider = Build();

        await using var scope = provider.CreateAsyncScope();

        Assert.Equal("AssistantConnectionTester", scope.ServiceProvider.GetRequiredService<IAssistantConnectionTester>().GetType().Name);
    }

    [Fact]
    public async Task WithoutProvider_TheContainerBuildsAndTheEngineIsDisabled()
    {
        await using var provider = Build();

        await using var scope = provider.CreateAsyncScope();
        var engine = scope.ServiceProvider.GetRequiredService<IAssistantEngine>();

        Assert.Equal(AssistantAvailability.Disabled, await engine.GetAvailabilityAsync(CancellationToken.None));
        Assert.Null(await engine.ReplyAsync(new AssistantConversation([], "Hola"), CancellationToken.None));
    }

    [Theory]
    [InlineData("Ollama")]
    [InlineData("ollama")]
    public async Task WithOllama_TheEngineIsEnabledWithoutContactingTheServer(string name)
    {
        await using var provider = Build(("Ai:Provider", name), ("Ai:Model", "modelo-ficticio"), ("Ai:Endpoint", "http://localhost:11434/"));

        await using var scope = provider.CreateAsyncScope();

        Assert.Equal(AssistantAvailability.Enabled, await scope.ServiceProvider.GetRequiredService<IAssistantEngine>().GetAvailabilityAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData("OpenAI")]
    [InlineData("gemini")]
    [InlineData("groq")]
    public async Task WithOpenAIOrGemini_TheEngineIsEnabledWithoutContactingTheProvider(string name)
    {
        await using var provider = Build(("Ai:Provider", name), ("Ai:Model", "modelo-ficticio"), ("Ai:ApiKey", "clave-prueba-no-real"));

        await using var scope = provider.CreateAsyncScope();

        Assert.Equal(AssistantAvailability.Enabled, await scope.ServiceProvider.GetRequiredService<IAssistantEngine>().GetAvailabilityAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Lifetimes_RegistryAndQuotaAreSingletonsAndEngineAndToolsetAreScoped()
    {
        await using var provider = Build(("Ai:Provider", "Ollama"), ("Ai:Model", "m"), ("Ai:Endpoint", "http://localhost:11434/"));

        await using var first = provider.CreateAsyncScope();
        await using var second = provider.CreateAsyncScope();

        Assert.Same(first.ServiceProvider.GetRequiredService<IAssistantRuntimeRegistry>(), second.ServiceProvider.GetRequiredService<IAssistantRuntimeRegistry>());
        Assert.Same(first.ServiceProvider.GetRequiredService<IAssistantQuota>(), second.ServiceProvider.GetRequiredService<IAssistantQuota>());
        Assert.IsType<InMemoryAssistantQuota>(first.ServiceProvider.GetRequiredService<IAssistantQuota>());
        Assert.NotSame(first.ServiceProvider.GetRequiredService<IAssistantEngine>(), second.ServiceProvider.GetRequiredService<IAssistantEngine>());
        Assert.IsType<ChatAssistantEngine>(first.ServiceProvider.GetRequiredService<IAssistantEngine>());
        Assert.NotSame(first.ServiceProvider.GetRequiredService<AssistantToolset>(), second.ServiceProvider.GetRequiredService<AssistantToolset>());
    }

    [Fact]
    public async Task TheReloadPieces_AreSingletons()
    {
        await using var provider = Build(("Ai:Provider", "Ollama"), ("Ai:Model", "m"), ("Ai:Endpoint", "http://localhost:11434/"));

        Assert.IsType<ConfigurationAssistantEnvironment>(provider.GetRequiredService<IAssistantEnvironment>());
        Assert.Same(provider.GetRequiredService<IAssistantEnvironment>(), provider.GetRequiredService<IAssistantEnvironment>());
        Assert.IsType<AssistantRuntimeReloader>(provider.GetRequiredService<IAssistantRuntimeReloader>());
        Assert.Same(provider.GetRequiredService<IAssistantRuntimeReloader>(), provider.GetRequiredService<IAssistantRuntimeReloader>());
    }

    [Fact]
    public async Task TheAskUseCaseIsScoped()
    {
        await using var provider = Build();

        await using var first = provider.CreateAsyncScope();
        await using var second = provider.CreateAsyncScope();

        Assert.NotSame(first.ServiceProvider.GetRequiredService<AskAssistantUseCase>(), second.ServiceProvider.GetRequiredService<AskAssistantUseCase>());
    }

    [Fact]
    public async Task TheProtector_IsASingletonThatReflectsTheMasterKeys()
    {
        await using var without = Build();
        await using var with = Build(("Ai:MasterKeys:1", Convert.ToBase64String(new byte[32])), ("Ai:ActiveMasterKeyVersion", "1"));

        var protector = without.GetRequiredService<IAssistantCredentialProtector>();

        Assert.Same(protector, without.GetRequiredService<IAssistantCredentialProtector>());
        Assert.False(protector.IsAvailable);
        Assert.True(with.GetRequiredService<IAssistantCredentialProtector>().IsAvailable);
    }

    [Theory]
    [InlineData("Anthropic", null, "Ai:ApiKey")]
    [InlineData("GitHubModels", "x", "Ai:Provider")]
    [InlineData("1", "x", "Ai:Provider")]
    public async Task WithAnInvalidConfiguration_TheOptionsFailWithTheKeyName(string provider, string? apiKey, string expectedKey)
    {
        await using var provider0 = Build(("Ai:Provider", provider), ("Ai:Model", "m"), ("Ai:ApiKey", apiKey));

        var failure = Assert.Throws<OptionsValidationException>(() => provider0.GetRequiredService<IOptions<AiOptions>>().Value);

        Assert.Contains(failure.Failures, text => text.StartsWith(expectedKey, StringComparison.Ordinal));
    }
}
