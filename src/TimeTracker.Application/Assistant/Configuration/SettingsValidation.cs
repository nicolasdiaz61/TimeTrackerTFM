namespace TimeTracker.Application.Assistant.Configuration;

public sealed record ValidatedSettingsInput(
    AssistantProvider Provider,
    string? Model,
    int? TimeoutSeconds,
    int? MaxOutputTokens,
    AssistantEffort? Effort,
    AssistantCredential? Credential);

public sealed record SettingsValidation(IReadOnlyList<FieldError> Errors, ValidatedSettingsInput? Value)
{
    public bool IsValid => Errors.Count == 0;
}
