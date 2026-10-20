using System.Reflection;

namespace TimeTracker.ArchitectureTests;

public class DependencyRuleTests
{
    private const string Prefix = "TimeTracker.";

    // Regla de dependencias hacia el dominio: cada capa solo puede referenciar las de dentro.
    private static readonly Dictionary<string, string[]> AllowedReferences = new()
    {
        ["TimeTracker.Domain"] = [],
        ["TimeTracker.Application"] = ["TimeTracker.Domain"],
        ["TimeTracker.Infrastructure"] = ["TimeTracker.Application", "TimeTracker.Domain"],
        ["TimeTracker.Api"] = ["TimeTracker.Application", "TimeTracker.Infrastructure", "TimeTracker.Domain"],
    };

    public static TheoryData<string> ProductionAssemblies => [.. AllowedReferences.Keys];

    [Theory]
    [MemberData(nameof(ProductionAssemblies))]
    public void ProductionAssembly_ReferencesOnlyAllowedAssemblies(string assemblyName)
    {
        var assembly = Assembly.Load(assemblyName);

        var illegal = assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => name.StartsWith(Prefix, StringComparison.Ordinal))
            .Except(AllowedReferences[assemblyName])
            .ToList();

        Assert.True(illegal.Count == 0, $"{assemblyName} referencia ensamblados no permitidos: {string.Join(", ", illegal)}");
    }

    [Fact]
    public void Domain_ReferencesOnlyBaseFramework()
    {
        var illegal = Assembly.Load("TimeTracker.Domain").GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => !IsBaseFramework(name))
            .ToList();

        Assert.True(illegal.Count == 0, $"TimeTracker.Domain referencia ensamblados ajenos al marco base: {string.Join(", ", illegal)}");
    }

    [Fact]
    public void Application_ReferencesOnlyDomainAndBaseFramework()
    {
        var illegal = Assembly.Load("TimeTracker.Application").GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => name != "TimeTracker.Domain" && !IsBaseFramework(name))
            .ToList();

        Assert.True(illegal.Count == 0, $"TimeTracker.Application referencia ensamblados ajenos al marco base y a Domain: {string.Join(", ", illegal)}");
    }

    // DD-1 y DD-16: los paquetes de proveedores de IA (y su contrato) solo se usan en Infrastructure; Api compone, no los toca.
    [Theory]
    [InlineData("TimeTracker.Domain")]
    [InlineData("TimeTracker.Application")]
    [InlineData("TimeTracker.Api")]
    public void ProviderSdks_AreOnlyReferencedByInfrastructure(string assemblyName)
    {
        string[] providerSdks = ["OpenAI", "Anthropic", "OllamaSharp", "Microsoft.Extensions.AI.OpenAI", "System.ClientModel"];

        var illegal = Assembly.Load(assemblyName).GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => providerSdks.Contains(name, StringComparer.Ordinal))
            .ToList();

        Assert.True(illegal.Count == 0, $"{assemblyName} referencia SDK de proveedores de IA: {string.Join(", ", illegal)}");
    }

    // Coverlet instrumenta el ensamblado e inyecta una referencia a `netstandard`; también es marco base.
    private static bool IsBaseFramework(string name)
        => name.StartsWith("System", StringComparison.Ordinal) || name == "netstandard";
}
