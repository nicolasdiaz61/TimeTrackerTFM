namespace TimeTracker.Application.Assistant.Configuration;

public interface IAssistantEnvironment
{
    AssistantEnvironmentDefaults Current { get; }
}

public interface IAssistantRuntimeReloader
{
    Task ReloadAsync(CancellationToken cancellationToken);
}
