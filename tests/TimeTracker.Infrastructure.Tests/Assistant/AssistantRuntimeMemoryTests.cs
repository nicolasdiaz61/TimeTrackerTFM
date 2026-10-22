using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using TimeTracker.Application.Assistant.Configuration;
using TimeTracker.Infrastructure.Assistant;
using TimeTracker.Infrastructure.Tests.Fakes;

using Xunit.Abstractions;

namespace TimeTracker.Infrastructure.Tests.Assistant;

/// <summary>
/// V-8 (tarea 4b.5): medición reproducible de la memoria de un pipeline y un cliente del proveedor por compañía con 1, 20 y 100
/// compañías cargadas. Usa la fábrica real de clientes (sin llamar nunca al proveedor: la red solo se abre al preguntar) y claves
/// inventadas. La prueba escribe las cifras en la salida y solo exige una cota holgada, para que un cambio que dispare el consumo se note.
/// </summary>
public sealed class AssistantRuntimeMemoryTests(ITestOutputHelper output)
{
    private const string Key = "clave-prueba-123456";

    // Cota deliberadamente holgada (el plan de Railway tiene una memoria mucho mayor): vigila una regresión grande, no mide el óptimo.
    private const long MaxBytesPerLoadedCompany = 2 * 1024 * 1024;

    private static AesGcmCredentialProtector Protector()
    {
        var options = new AiOptions { ActiveMasterKeyVersion = "1" };
        options.MasterKeys["1"] = Convert.ToBase64String(new byte[32]);
        return new AesGcmCredentialProtector(Options.Create(options));
    }

    private static long MeasureBytesForCompanies(int count, ITestOutputHelper output)
    {
        var environment = new AiOptions { AllowedProviders = "Anthropic,OpenAI,Gemini", MaxLoadedCompanies = 1000 };
        var options = Options.Create(environment);
        var protector = Protector();
        var reader = new FakeCompanySettingsReader();
        var companies = Enumerable.Range(1, count).Select(number => Guid.Parse($"00000000-0000-4000-8000-{number:D12}")).ToList();
        foreach (var (company, index) in companies.Select((company, index) => (company, index)))
        {
            var provider = (index % 3) switch { 0 => AssistantProvider.Anthropic, 1 => AssistantProvider.OpenAI, _ => AssistantProvider.Gemini };
            reader.Set(
                company,
                new AssistantSettings(provider, "modelo-ficticio", null, null, null, protector.Protect(new AssistantCredential(Key), provider, company), Guid.NewGuid(), DateTimeOffset.UnixEpoch, "7"),
                usesPlatform: false);
        }

        using var registry = new AssistantRuntimeRegistry(
            reader,
            new AssistantOptionsResolver(options, new ConfigurationAssistantEnvironment(options), protector),
            new DefaultChatClientFactory(),
            options,
            new FakeTimeProvider(),
            new CapturingLogger<AssistantRuntimeRegistry>());

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var before = GC.GetTotalMemory(forceFullCollection: true);
        foreach (var company in companies)
        {
            Assert.NotNull(registry.GetAsync(company, CancellationToken.None).AsTask().GetAwaiter().GetResult().Pipeline);
        }

        var after = GC.GetTotalMemory(forceFullCollection: true);
        Assert.Equal(count, registry.Loaded().Count);
        output.WriteLine($"V-8: {count,3} compañías con proveedor cargado -> {(after - before) / 1024.0:N0} KiB ({(after - before) / (double)count / 1024.0:N1} KiB por compañía)");
        GC.KeepAlive(registry);
        return after - before;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    [InlineData(100)]
    public void LoadedPipelines_StayWellBelowTheBudgetPerCompany(int count)
    {
        var bytes = MeasureBytesForCompanies(count, output);

        // Solo cota superior: con cobertura o recolecciones concurrentes la diferencia puntual puede salir pequeña o incluso negativa.
        Assert.True(bytes <= MaxBytesPerLoadedCompany * count, $"{bytes} bytes para {count} compañías");
    }
}
