namespace TimeTracker.Application.Assistant.Configuration;

public abstract record TestAssistantConnectionResult
{
    private TestAssistantConnectionResult()
    {
    }

    /// <summary>La prueba se completó; un fallo del proveedor es un resultado, no un error de la petición.</summary>
    public sealed record Completed(ConnectionTestResult Outcome) : TestAssistantConnectionResult;

    public sealed record Invalid(IReadOnlyList<FieldError> Errors) : TestAssistantConnectionResult;
}

/// <summary>Prueba valores candidatos sin guardarlos ni auditarlos; lo que falte se completa con la configuración efectiva.</summary>
public sealed class TestAssistantConnectionUseCase(
    IAssistantSettingsStore store,
    IAssistantCredentialProtector protector,
    IAssistantEnvironment environment,
    IAssistantConnectionTester tester)
{
    private static readonly FieldError NothingToTest = new("provider", "El campo provider debe indicar un proveedor: con el asistente deshabilitado no hay conexión que probar.");

    public async Task<TestAssistantConnectionResult> ExecuteAsync(AssistantSettingsInput input, CancellationToken cancellationToken)
    {
        var env = environment.Current;
        var stored = await store.GetAsync(cancellationToken);
        var effective = AssistantSettingsView.Build(env, protector, stored).Effective;

        var validation = AssistantSettingsRules.Validate(Complete(input, effective), env, stored, protector);
        if (validation.Value is not { } value)
        {
            return new TestAssistantConnectionResult.Invalid(validation.Errors);
        }

        if (value.Provider == AssistantProvider.None)
        {
            return new TestAssistantConnectionResult.Invalid([NothingToTest]);
        }

        var candidate = new AssistantConnectionCandidate(
            value.Provider,
            value.Model ?? env.Model!,
            value.TimeoutSeconds ?? effective.TimeoutSeconds,
            value.MaxOutputTokens ?? effective.MaxOutputTokens,
            value.Effort,
            value.Credential ?? FindCredential(value.Provider, env, stored));
        return new TestAssistantConnectionResult.Completed(await tester.TestAsync(candidate, cancellationToken));
    }

    private static AssistantSettingsInput Complete(AssistantSettingsInput input, EffectiveAssistantSettings effective)
    {
        var sameProvider = input.Provider is null || string.Equals(input.Provider, effective.Provider.ToString(), StringComparison.OrdinalIgnoreCase);
        return input with
        {
            Provider = input.Provider ?? effective.Provider.ToString(),
            Model = input.Model ?? effective.Model,
            TimeoutSeconds = input.TimeoutSeconds ?? effective.TimeoutSeconds,
            MaxOutputTokens = input.MaxOutputTokens ?? effective.MaxOutputTokens,
            Effort = input.Effort ?? (sameProvider ? effective.Effort?.ToString() : null),
        };
    }

    private AssistantCredential? FindCredential(AssistantProvider provider, AssistantEnvironmentDefaults env, AssistantSettings? stored)
        => UsableCredential.FromStored(stored, provider, protector) ?? UsableCredential.FromEnvironment(env, provider);
}
