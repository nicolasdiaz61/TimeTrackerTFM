using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using TimeTracker.Application.Assistant;
using TimeTracker.Application.Identity;
using TimeTracker.Application.Team;
using TimeTracker.Application.Tests.Fakes;
using TimeTracker.Application.TimeTracking;
using TimeTracker.Application.Users;
using TimeTracker.Domain.Users;
using TimeTracker.Infrastructure.Assistant;
using TimeTracker.Infrastructure.Tests.Fakes;

namespace TimeTracker.Infrastructure.Tests.Assistant;

/// <summary>
/// Fija el ciclo de vida del motor: el registro de instantáneas es singleton y el motor, igual que el conjunto de
/// herramientas, es de ámbito de petición (depende de la identidad y de los repositorios del ámbito).
/// </summary>
public sealed class EngineLifetimeTests
{
    private static ServiceProvider Build(Action<IServiceCollection> registerEngine)
    {
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(new FakeTimeProvider());
        services.AddLogging();
        services.AddSingleton(Options.Create(new AiOptions { Provider = "Ollama", Model = "m", Endpoint = "http://localhost:11434/" }));
        services.AddSingleton<IAssistantRuntimeRegistry>(provider => new FakeRuntimeRegistry(new AssistantChatClient(
            new FakeChatClient(_ => new ChatResponse(new ChatMessage(ChatRole.Assistant, "hola"))),
            provider.GetRequiredService<IOptions<AiOptions>>().Value)));
        services.AddScoped<ICurrentUser>(_ => new FixedCurrentUser(Guid.NewGuid(), UserRole.Employee));
        services.AddScoped<ITimeEntryRepository, InMemoryTimeEntryRepository>();
        services.AddScoped<IUserRepository, InMemoryUserRepository>();
        services.AddScoped<GetTimeEntriesUseCase>();
        services.AddScoped<GetTeamMembersUseCase>();
        services.AddScoped<GetTeamMemberTimeEntriesUseCase>();
        services.AddScoped<AssistantToolset>();
        registerEngine(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    [Fact]
    public async Task ScopedEngine_WithSingletonClientAndScopedToolset_ResolvesAndReplies()
    {
        await using var provider = Build(services => services.AddScoped<IAssistantEngine, ChatAssistantEngine>());

        await using var scope = provider.CreateAsyncScope();
        var reply = await scope.ServiceProvider.GetRequiredService<IAssistantEngine>()
            .ReplyAsync(new AssistantConversation([], "Hola"), CancellationToken.None);

        Assert.Equal("hola", reply);
    }

    [Fact]
    public void SingletonEngine_CannotCaptureTheScopedToolset()
    {
        var failure = Record.Exception(() => Build(services => services.AddSingleton<IAssistantEngine, ChatAssistantEngine>()));

        Assert.NotNull(failure);
    }
}
