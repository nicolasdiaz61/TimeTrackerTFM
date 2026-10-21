using TimeTracker.Application.Identity;

namespace TimeTracker.Application.Assistant.Configuration;

public sealed class GetAssistantSettingsUseCase(IAssistantSettingsStore store, IAssistantCredentialProtector protector, IAssistantEnvironment environment, ICurrentUser currentUser)
{
    public async Task<AssistantSettingsView> ExecuteAsync(CancellationToken cancellationToken)
        => AssistantSettingsView.Build(environment.Current, protector, await store.GetAsync(cancellationToken), currentUser.CompanyId);
}
