using System.Text.RegularExpressions;

namespace TimeTracker.ArchitectureTests;

/// <summary>
/// <c>ICompanyScopeFactory.CreateScope(companyId)</c> funciona como un localizador de servicios acotado (excepción a AGENTS §3,
/// DD-7, ADR 0020): solo puede invocarse desde esta lista cerrada de archivos de <c>src/</c>, cada uno con su motivo. Un llamador
/// nuevo obliga a tocar esta prueba y a revisar la decisión. Las pruebas de integración la usan libremente (solo se analiza src/).
/// </summary>
public partial class CreateScopeClosedListTests
{
    // Cada tarea posterior que añada un llamador lo declara aquí. La 5.5 declaró el almacén de registro, la 5.11b el validador de usuarios demo
    // y la 4b retiró el puente y declaró el registro de instantáneas.
    private static readonly Dictionary<string, string> Allowed = new()
    {
        ["src/TimeTracker.Infrastructure/Assistant/CompanySettingsReader.cs"] =
            "Registro de instantáneas del asistente (DD-14): lee la configuración de la compañía que pregunta o que el sondeo ha visto cambiar, fuera de una petición HTTP.",
        ["src/TimeTracker.Infrastructure/Persistence/CompanyRegistrationStore.cs"] =
            "Autorregistro (DD-19): la petición es anónima y no tiene compañía; el almacén escribe compañía y Manager dentro del ámbito de la compañía nueva.",
        ["src/TimeTracker.Api/Registration/DemoUsersStartupGuard.cs"] =
            "Validador de arranque (DD-22): busca usuarios de demostración en la compañía por defecto, fuera de una petición HTTP y sin IgnoreQueryFilters.",
        ["src/TimeTracker.Api/Seeding/DemoUsersSeeder.cs"] = "La siembra de usuarios de demostración actúa como la compañía por defecto.",
        ["src/TimeTracker.Api/Seeding/DemoDataSeeder.cs"] = "La siembra de fichajes de demostración actúa como la compañía por defecto.",
    };

    [Fact]
    public void CreateScope_IsCalledOnlyFromTheClosedList()
    {
        var callers = CallersOfCreateScope();

        var unexpected = callers.Except(Allowed.Keys).ToList();
        Assert.True(unexpected.Count == 0, $"Llaman a CreateScope(companyId) fuera de la lista cerrada (DD-7): {string.Join(", ", unexpected)}");
    }

    [Fact]
    public void ClosedList_HasNoEntryWithoutUseAndEveryEntryStatesItsReason()
    {
        var callers = CallersOfCreateScope();

        var unused = Allowed.Keys.Except(callers).ToList();
        Assert.True(unused.Count == 0, $"Entradas de la lista cerrada sin uso: {string.Join(", ", unused)}");
        Assert.All(Allowed.Values, reason => Assert.False(string.IsNullOrWhiteSpace(reason)));
    }

    [Theory]
    [InlineData("await using var scope = factory.CreateScope(Company.DefaultId);")]
    [InlineData("var scope = scopeFactory . CreateScope (companyId);")]
    [InlineData("using var s = companyScopes.CreateScope(\n    id);")]
    public void Scanner_DetectsACallWithAnArgument(string source) => Assert.True(CreatesCompanyScope(source));

    [Theory]
    [InlineData("using var scope = provider.CreateScope();")]
    [InlineData("await using var scope = provider.CreateAsyncScope();")]
    [InlineData("    AsyncServiceScope CreateScope(Guid companyId);")]
    [InlineData("    public AsyncServiceScope CreateScope(Guid companyId)")]
    [InlineData("    /// <see cref=\"ICompanyScopeFactory.CreateScope(Guid)\"/>")]
    [InlineData("    // Llama a CreateScope(companyId) en otro sitio")]
    public void Scanner_IgnoresDeclarationsCommentsAndScopesWithoutCompany(string source) => Assert.False(CreatesCompanyScope(source));

    private static List<string> CallersOfCreateScope()
    {
        var src = Path.Combine(RepositoryRoot.Path, "src");
        return Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => CreatesCompanyScope(File.ReadAllText(file)))
            .Select(file => Path.GetRelativePath(RepositoryRoot.Path, file).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    // Guarda sintáctica: una llamada con argumento a un método llamado CreateScope. Se ignoran comentarios y la propia declaración.
    private static bool CreatesCompanyScope(string source)
    {
        var code = string.Join('\n', source.Split('\n').Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));
        return CallPattern().Matches(code).Any(match => !IsDeclaration(code, match.Index));
    }

    private static bool IsDeclaration(string code, int index)
    {
        var before = code[..index].TrimEnd();
        return before.EndsWith("AsyncServiceScope", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"\bCreateScope\s*\(\s*[^)\s]")]
    private static partial Regex CallPattern();
}
