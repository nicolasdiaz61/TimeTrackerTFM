using System.Reflection;

using Microsoft.Extensions.Time.Testing;

using TimeTracker.Application.Assistant.Configuration;
using TimeTracker.Application.Tests.Fakes;
using TimeTracker.Domain.Companies;
using TimeTracker.Domain.Users;

using static TimeTracker.Application.Tests.Assistant.Configuration.ConfigurationTestData;

namespace TimeTracker.Application.Tests.Assistant.Configuration;

/// <summary>
/// Los cuatro casos de uso trabajan con la compañía del contexto (<c>ICurrentUser</c>), nunca con una indicada por el comando
/// (DD-13, TC-9). El almacén real acota la fila por el filtro global; aquí se comprueba lo que decide Application: si la compañía
/// es de plataforma, qué clave puede usar y a qué compañía se recarga.
/// </summary>
public class AssistantUseCasesCompanyTests
{
    private static readonly Guid Ana = Guid.NewGuid();
    private static readonly Guid CompanyB = Guid.Parse("22222222-2222-7222-8222-222222222222");

    private readonly InMemorySettingsStore _store = new();
    private readonly FakeProtector _protector = new();
    private readonly FakeReloader _reloader = new();
    private readonly FakeTester _tester = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 19, 9, 0, 0, TimeSpan.Zero));
    private readonly AssistantEnvironmentDefaults _env = Env(provider: AssistantProvider.Anthropic, model: "env-m", apiKey: SecretKey, allowedProviders: "Anthropic,OpenAI");

    private FixedCurrentUser UserOf(Guid companyId) => new(Ana, UserRole.Manager, companyId);

    private Task<AssistantSettingsView> Get(Guid companyId)
        => new GetAssistantSettingsUseCase(_store, _protector, new FakeEnvironment(_env), UserOf(companyId), PlatformCompanies()).ExecuteAsync(CancellationToken.None);

    private Task<SaveAssistantSettingsResult> Save(Guid companyId, AssistantSettingsInput input, string token = "0")
        => new SaveAssistantSettingsUseCase(_store, _protector, new FakeEnvironment(_env), _reloader, UserOf(companyId), _time, PlatformCompanies())
            .ExecuteAsync(new SaveAssistantSettingsCommand(input, token), CancellationToken.None);

    private Task<DeleteOutcome> Reset(Guid companyId)
        => new ResetAssistantSettingsUseCase(_store, _reloader, UserOf(companyId), _time).ExecuteAsync(CancellationToken.None);

    private Task<TestAssistantConnectionResult> TestConnection(Guid companyId, AssistantSettingsInput input)
        => new TestAssistantConnectionUseCase(_store, _protector, new FakeEnvironment(_env), _tester, UserOf(companyId), PlatformCompanies())
            .ExecuteAsync(input, CancellationToken.None);

    [Fact]
    public async Task GetWithoutARow_DependsOnTheCompanyOfTheContext()
    {
        var platform = await Get(Company.DefaultId);
        var other = await Get(CompanyB);

        Assert.Equal((AssistantProvider.Anthropic, "env-m", true, SettingSource.Env), (platform.Effective.Provider, platform.Effective.Model, platform.Effective.CredentialConfigured, platform.Effective.Sources.Provider));
        Assert.Equal((AssistantProvider.None, null, false, SettingSource.Default), (other.Effective.Provider, other.Effective.Model, other.Effective.CredentialConfigured, other.Effective.Sources.Provider));
        Assert.Equal(SettingSource.None, other.Effective.Sources.Credential);
    }

    [Fact]
    public async Task ATestWithoutACandidateKey_InANonPlatformCompany_NeverUsesThePlatformKey()
    {
        var result = await TestConnection(CompanyB, new AssistantSettingsInput("Anthropic", "m-1", null, null, null, null));

        Assert.Equal(["credential"], Assert.IsType<TestAssistantConnectionResult.Invalid>(result).Errors.Select(error => error.Field));
        Assert.Empty(_tester.Received);
    }

    [Fact]
    public async Task ATestWithoutACandidateKey_InThePlatformCompany_UsesThePlatformKey()
    {
        var result = await TestConnection(Company.DefaultId, new AssistantSettingsInput("Anthropic", "m-1", null, null, null, null));

        Assert.IsType<TestAssistantConnectionResult.Completed>(result);
        Assert.Equal(SecretKey, Assert.Single(_tester.Received).Credential!.Value);
    }

    [Fact]
    public async Task ATestWithoutACandidateKey_UsesTheStoredKeyOfTheOwnCompanyAndNotOneProtectedForAnother()
    {
        _store.Row = Stored(AssistantProvider.OpenAI, credential: _protector.Protect(new AssistantCredential("clave-propia-0123456"), AssistantProvider.OpenAI, CompanyB));
        var otherCompanysKey = _protector.Protect(new AssistantCredential("clave-ajena-0123456"), AssistantProvider.OpenAI, Company.DefaultId);

        var own = await TestConnection(CompanyB, new AssistantSettingsInput("OpenAI", "m-1", null, null, null, null));
        _store.Row = Stored(AssistantProvider.OpenAI, credential: otherCompanysKey);
        var foreign = await TestConnection(CompanyB, new AssistantSettingsInput("OpenAI", "m-1", null, null, null, null));

        Assert.IsType<TestAssistantConnectionResult.Completed>(own);
        Assert.Equal("clave-propia-0123456", Assert.Single(_tester.Received).Credential!.Value);
        Assert.IsType<TestAssistantConnectionResult.Invalid>(foreign);
    }

    [Fact]
    public async Task ACandidateKey_IsTheOneUsed()
    {
        await TestConnection(CompanyB, new AssistantSettingsInput("OpenAI", "m-1", null, null, null, "clave-candidata-0123"));

        Assert.Equal("clave-candidata-0123", Assert.Single(_tester.Received).Credential!.Value);
    }

    [Fact]
    public async Task SaveAndReset_ReloadTheCompanyOfTheContext()
    {
        var saved = await Save(CompanyB, new AssistantSettingsInput("OpenAI", "m-1", null, null, null, SecretKey));
        Assert.IsType<SaveAssistantSettingsResult.Saved>(saved);
        Assert.Equal(DeleteOutcome.Deleted, await Reset(CompanyB));
        await Save(Company.DefaultId, new AssistantSettingsInput("OpenAI", "m-1", null, null, null, SecretKey));

        Assert.Equal([CompanyB, CompanyB, Company.DefaultId], _reloader.Companies);
    }

    [Fact]
    public async Task ASaveInANonPlatformCompany_BindsTheKeyToThatCompanyAndDoesNotInheritTheEnvironmentModel()
    {
        var withoutModel = await Save(CompanyB, new AssistantSettingsInput("OpenAI", null, null, null, null, SecretKey));
        var saved = await Save(CompanyB, new AssistantSettingsInput("OpenAI", "m-1", null, null, null, SecretKey));

        Assert.Equal(["model"], Assert.IsType<SaveAssistantSettingsResult.Invalid>(withoutModel).Errors.Select(error => error.Field));
        Assert.IsType<SaveAssistantSettingsResult.Saved>(saved);
        Assert.True(_protector.TryUnprotect(_store.Row!.Credential!, AssistantProvider.OpenAI, CompanyB, out _));
        Assert.False(_protector.TryUnprotect(_store.Row.Credential!, AssistantProvider.OpenAI, Company.DefaultId, out _));
    }

    [Fact]
    public async Task AReloadFailure_KeepsTheSaveAndTheResetSuccessful()
    {
        _reloader.Fails = true;

        Assert.IsType<SaveAssistantSettingsResult.Saved>(await Save(CompanyB, new AssistantSettingsInput("OpenAI", "m-1", null, null, null, SecretKey)));
        Assert.Equal(DeleteOutcome.Deleted, await Reset(CompanyB));
        Assert.Equal(2, _reloader.Calls);
    }

    [Theory]
    [InlineData(typeof(GetAssistantSettingsUseCase))]
    [InlineData(typeof(SaveAssistantSettingsUseCase))]
    [InlineData(typeof(ResetAssistantSettingsUseCase))]
    [InlineData(typeof(TestAssistantConnectionUseCase))]
    public void TheUseCaseSignatures_HaveNoCompanyParameter(Type useCase)
    {
        var methods = useCase.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.NotEmpty(methods);
        Assert.DoesNotContain(methods.SelectMany(method => method.GetParameters()), parameter => parameter.ParameterType == typeof(Guid) || parameter.Name!.Contains("company", StringComparison.OrdinalIgnoreCase));
        var command = typeof(SaveAssistantSettingsCommand).GetProperties();
        Assert.DoesNotContain(command, property => property.Name.Contains("Company", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeof(AssistantSettingsInput).GetProperties(), property => property.Name.Contains("Company", StringComparison.OrdinalIgnoreCase));
    }
}
