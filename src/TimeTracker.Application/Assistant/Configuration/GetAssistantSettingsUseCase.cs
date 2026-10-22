using TimeTracker.Application.Companies;
using TimeTracker.Application.Identity;

namespace TimeTracker.Application.Assistant.Configuration;

public sealed class GetAssistantSettingsUseCase(
    IAssistantSettingsStore store,
    IAssistantCredentialProtector protector,
    IAssistantEnvironment environment,
    ICurrentUser currentUser,
    ICompanyRepository companies)
{
    public async Task<AssistantSettingsView> ExecuteAsync(CancellationToken cancellationToken)
    {
        var company = await AssistantCompany.ResolveAsync(companies, currentUser, cancellationToken);
        return AssistantSettingsView.Build(company.EnvironmentOf(environment), protector, await store.GetAsync(cancellationToken), company);
    }
}
