using System.Text.RegularExpressions;

namespace TimeTracker.ArchitectureTests;

/// <summary>
/// TC-6 (DD-9, spec company-isolation «Lista cerrada de usos de IgnoreQueryFilters»): <c>IgnoreQueryFilters</c> aparece solo en la
/// lista cerrada de sitios de <c>src/</c>, cada uno con <c>// company-filter-bypass: motivo</c>; las APIs que evitan el filtro están
/// prohibidas fuera de <c>Migrations/</c> (que es SQL controlado). Un sitio nuevo exige tocar esta prueba y revisar la decisión.
/// </summary>
public partial class IgnoreQueryFiltersClosedListTests
{
    // Lista cerrada: archivo -> número exacto de usos. `CompanyRepository.FindLoginInfoAsync` no es un sitio: lee `companies` por
    // clave primaria y `companies` no lleva filtro.
    private static readonly Dictionary<string, int> Allowed = new()
    {
        // Sitio 1: el login no tiene compañía y el correo es único global.
        ["src/TimeTracker.Infrastructure/Persistence/UserRepository.cs"] = 1,
        // Sitio 2: el sondeo global lee (company_id, xmin) de todas las compañías. Se implementa en la tarea 4b.7.
        ["src/TimeTracker.Infrastructure/Persistence/AssistantSettingsStore.cs"] = 1,
    };

    // TRANSITORIO (tarea 3.6, mantenida en la 4a): `AssistantSettingsStore.ListVersionsAsync` nace en la tarea 4b.7 (la 4a generaliza el almacén
    // por la compañía del contexto, sin necesidad de un segundo `IgnoreQueryFilters`), así que hasta entonces su entrada de la
    // lista no tiene uso. La tolerancia caduca sola: en cuanto la entrada tenga uso, `TheTransitionalTolerance_ExpiresAsSoonAsAnEntryGetsAUse`
    // falla hasta que la 4b.7 la retire y la lista quede con 2 sitios y ninguna entrada sin uso.
    private static readonly string[] TransitionallyUnused = ["src/TimeTracker.Infrastructure/Persistence/AssistantSettingsStore.cs"];

    private static readonly string[] ProhibitedApis =
    [
        @"\.Find\s*\(",
        @"\.FindAsync\s*\(",
        @"\bFromSql\w*\s*\(",
        @"\bExecuteSql\w*\s*\(",
        @"\bSqlQuery\w*\s*[<(]",
        @"\bExecuteUpdate\w*\s*\(",
        @"\bExecuteDelete\w*\s*\(",
        @"\bGetDbConnection\s*\(",
        @"\bNpgsql(Connection|Command|DataSource|Batch|DataReader)\b",
    ];

    [Fact]
    public void IgnoreQueryFilters_IsUsedOnlyInTheClosedList_WithTheExactCount()
    {
        var usage = CountUsagePerFile();

        var unexpected = usage.Keys.Except(Allowed.Keys).ToList();
        Assert.True(unexpected.Count == 0, $"IgnoreQueryFilters fuera de la lista cerrada (DD-9): {string.Join(", ", unexpected)}");

        var wrongCount = Allowed
            .Where(entry => !TransitionallyUnused.Contains(entry.Key) || usage.ContainsKey(entry.Key))
            .Where(entry => usage.GetValueOrDefault(entry.Key) != entry.Value)
            .Select(entry => $"{entry.Key} (esperado {entry.Value}, hay {usage.GetValueOrDefault(entry.Key)})")
            .ToList();
        Assert.True(wrongCount.Count == 0, $"Recuento distinto del de la lista cerrada: {string.Join(", ", wrongCount)}");
    }

    [Fact]
    public void ClosedList_HasNoEntryWithoutUse_ExceptTheDeclaredTransitionalOne()
    {
        var usage = CountUsagePerFile();

        var unused = Allowed.Keys.Where(file => !usage.ContainsKey(file)).Except(TransitionallyUnused).ToList();

        Assert.True(unused.Count == 0, $"Entradas de la lista cerrada sin uso: {string.Join(", ", unused)}");
        Assert.Single(TransitionallyUnused);
    }

    [Fact]
    public void TheTransitionalTolerance_ExpiresAsSoonAsAnEntryGetsAUse()
    {
        var usage = CountUsagePerFile();

        var alreadyUsed = TransitionallyUnused.Where(usage.ContainsKey).ToList();

        Assert.True(
            alreadyUsed.Count == 0,
            $"La entrada transitoria ya tiene uso: retire la tolerancia TransitionallyUnused de esta prueba (la lista queda con todas sus entradas en uso): {string.Join(", ", alreadyUsed)}");
    }

    [Fact]
    public void EveryUse_CarriesTheCompanyFilterBypassCommentWithItsReason()
    {
        var missing = SourceFiles()
            .Where(file => UsesWithoutBypassComment(File.ReadAllText(file)) > 0)
            .Select(RelativePath)
            .ToList();

        Assert.True(missing.Count == 0, $"IgnoreQueryFilters sin «// company-filter-bypass: motivo»: {string.Join(", ", missing)}");
    }

    [Fact]
    public void ApisThatBypassTheFilter_AreNotUsedOutsideMigrations()
    {
        var offenders = SourceFiles()
            .Where(file => !IsMigration(file))
            .SelectMany(file => ProhibitedApis.Where(pattern => Regex.IsMatch(StripComments(File.ReadAllText(file)), pattern)).Select(pattern => $"{RelativePath(file)} ({pattern})"))
            .ToList();

        Assert.True(offenders.Count == 0, $"APIs que evitan el filtro de compañía (DD-9): {string.Join(", ", offenders)}");
    }

    [Theory]
    [InlineData("var user = await context.Users.IgnoreQueryFilters().FirstAsync();", 1)]
    [InlineData("context.Users\n    .IgnoreQueryFilters ()\n    .Where(x => x)", 1)]
    [InlineData("a.IgnoreQueryFilters(); b.IgnoreQueryFilters();", 2)]
    [InlineData("// context.Users.IgnoreQueryFilters()", 0)]
    [InlineData("/// <see cref=\"X.IgnoreQueryFilters()\"/>", 0)]
    [InlineData("var text = 1; // usa IgnoreQueryFilters() aquí", 0)]
    [InlineData("var query = context.Users.Where(x => true);", 0)]
    public void UseScanner_CountsRealCallsAndIgnoresComments(string source, int expected) => Assert.Equal(expected, CountUses(source));

    [Theory]
    [InlineData("// company-filter-bypass: el login no tiene compañía\nvar user = context.Users.IgnoreQueryFilters().First();", 0)]
    [InlineData("var user = context.Users.IgnoreQueryFilters().First(); // company-filter-bypass: motivo", 0)]
    [InlineData("var user = context.Users.IgnoreQueryFilters().First();", 1)]
    [InlineData("// company-filter-bypass:\nvar user = context.Users.IgnoreQueryFilters().First();", 1)]
    [InlineData("// company-filter-bypass: motivo\n\n\n\n\n\nvar user = context.Users.IgnoreQueryFilters().First();", 1)]
    public void BypassCommentScanner_RequiresAReasonNextToEachUse(string source, int expectedWithout) => Assert.Equal(expectedWithout, UsesWithoutBypassComment(source));

    [Theory]
    [InlineData("context.Users.Find(id)", @"\.Find\s*\(", true)]
    [InlineData("await context.Users.FindAsync(id)", @"\.FindAsync\s*\(", true)]
    [InlineData("context.Database.ExecuteSqlRaw(sql)", @"\bExecuteSql\w*\s*\(", true)]
    [InlineData("context.Users.Where(x => x).ExecuteDeleteAsync()", @"\bExecuteDelete\w*\s*\(", true)]
    [InlineData("// context.Users.Find(id)", @"\.Find\s*\(", false)]
    [InlineData("var found = list.FirstOrDefault(x => x.Id == id);", @"\.Find\s*\(", false)]
    [InlineData("catch (DbUpdateException exception) when (exception.InnerException is PostgresException)", @"\bNpgsql(Connection|Command|DataSource|Batch|DataReader)\b", false)]
    [InlineData("await using var connection = new NpgsqlConnection(text);", @"\bNpgsql(Connection|Command|DataSource|Batch|DataReader)\b", true)]
    public void ProhibitedScanner_DetectsCodeAndIgnoresCommentsAndPostgresException(string source, string pattern, bool expected)
    {
        Assert.Contains(pattern, ProhibitedApis);
        Assert.Equal(expected, Regex.IsMatch(StripComments(source), pattern));
    }

    private static Dictionary<string, int> CountUsagePerFile()
        => SourceFiles()
            .Select(file => (File: RelativePath(file), Count: CountUses(File.ReadAllText(file))))
            .Where(item => item.Count > 0)
            .ToDictionary(item => item.File, item => item.Count);

    private static int CountUses(string source) => UsePattern().Matches(StripComments(source)).Count;

    // Cada uso lleva su comentario de bypass en la misma línea o en las cuatro anteriores (una sentencia encadenada ocupa varias líneas).
    private static int UsesWithoutBypassComment(string source)
    {
        var lines = source.Split('\n');
        var code = StripComments(source).Split('\n');
        return Enumerable.Range(0, lines.Length)
            .Where(index => UsePattern().IsMatch(code[index]))
            .Count(index => !lines[Math.Max(0, index - 4)..(index + 1)].Any(line => BypassPattern().IsMatch(line)));
    }

    private static string StripComments(string source)
        => string.Join('\n', source.Split('\n').Select(line =>
        {
            var comment = line.IndexOf("//", StringComparison.Ordinal);
            return comment < 0 ? line : line[..comment];
        }));

    private static IEnumerable<string> SourceFiles()
    {
        var separator = Path.DirectorySeparatorChar;
        return Directory.EnumerateFiles(Path.Combine(RepositoryRoot.Path, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{separator}obj{separator}", StringComparison.Ordinal)
                && !file.Contains($"{separator}bin{separator}", StringComparison.Ordinal));
    }

    private static bool IsMigration(string file) => file.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static string RelativePath(string file) => Path.GetRelativePath(RepositoryRoot.Path, file).Replace('\\', '/');

    [GeneratedRegex(@"\.\s*IgnoreQueryFilters\s*\(")]
    private static partial Regex UsePattern();

    [GeneratedRegex(@"//\s*company-filter-bypass:\s*\S")]
    private static partial Regex BypassPattern();
}
