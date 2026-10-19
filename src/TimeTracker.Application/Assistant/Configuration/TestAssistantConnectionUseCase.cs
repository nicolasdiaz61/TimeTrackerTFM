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
    private static readonly FieldError ModelRequired = new("model", "El campo model es obligatorio: no hay modelo guardado ni en el entorno.");

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

        if (value.Model is not { } model)
        {
            return new TestAssistantConnectionResult.Invalid([ModelRequired]);
        }

        var candidate = new AssistantConnectionCandidate(
            value.Provider,
            model,
            value.TimeoutSeconds ?? effective.TimeoutSeconds,
            value.MaxOutputTokens ?? effective.MaxOutputTokens,
            value.Effort ?? InheritedEffort(value.Provider, effective),
            value.Credential ?? FindCredential(value.Provider, env, stored));
        return new TestAssistantConnectionResult.Completed(await tester.TestAsync(candidate, cancellationToken));
    }

    /// <summary>Solo completa proveedor y modelo: lo heredado del entorno (esfuerzo, tiempo, tokens) no se valida como si lo enviara el Manager.</summary>
    private static AssistantSettingsInput Complete(AssistantSettingsInput input, EffectiveAssistantSettings effective)
        => input with
        {
            Provider = input.Provider ?? effective.Provider.ToString(),
            Model = input.Model ?? effective.Model,
        };

    private static AssistantEffort? InheritedEffort(AssistantProvider provider, EffectiveAssistantSettings effective)
        => provider == effective.Provider && AssistantProviderTraits.For(provider).SupportsEffort ? effective.Effort : null;

    private AssistantCredential? FindCredential(AssistantProvider provider, AssistantEnvironmentDefaults env, AssistantSettings? stored)
        => UsableCredential.FromStored(stored, provider, protector) ?? UsableCredential.FromEnvironment(env, provider);
}
