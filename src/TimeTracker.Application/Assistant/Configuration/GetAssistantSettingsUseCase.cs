namespace TimeTracker.Application.Assistant.Configuration;

public sealed class GetAssistantSettingsUseCase(IAssistantSettingsStore store, IAssistantCredentialProtector protector, IAssistantEnvironment environment)
{
    public async Task<AssistantSettingsView> ExecuteAsync(CancellationToken cancellationToken)
        => AssistantSettingsView.Build(environment.Current, protector, await store.GetAsync(cancellationToken));
}
