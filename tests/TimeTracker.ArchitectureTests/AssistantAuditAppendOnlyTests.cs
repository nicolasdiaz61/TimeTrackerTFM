using System.Reflection;
using System.Text.RegularExpressions;

using TimeTracker.Application.Assistant.Configuration;

namespace TimeTracker.ArchitectureTests;

/// <summary>La auditoría de la configuración del asistente solo admite inserción (DD-9): nada la actualiza ni la borra desde la aplicación.</summary>
public partial class AssistantAuditAppendOnlyTests
{
    private static readonly string[] AllowedStoreMembersThatTakeAnAudit = ["DeleteAsync", "SaveAsync"];

    [GeneratedRegex(@"\b(Remove|RemoveRange|Update|UpdateRange|ExecuteDelete|ExecuteDeleteAsync|ExecuteUpdate|ExecuteUpdateAsync)\b", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ForbiddenCall();

    [GeneratedRegex(@"\b(DELETE|UPDATE|TRUNCATE)\s+(FROM\s+)?""?assistant_settings_audit", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ForbiddenSql();

    [Fact]
    public void ProductionSource_NeverUpdatesOrDeletesTheAuditEntity()
    {
        var offending = ProductionSources()
            .SelectMany(file => FindOffendingStatements(File.ReadAllText(file)).Select(statement => $"{Path.GetFileName(file)}: {statement}"))
            .ToList();

        Assert.True(offending.Count == 0, $"Operaciones de modificación sobre la auditoría: {string.Join(" | ", offending)}");
    }

    [Theory]
    [InlineData("context.Remove(auditRow);")]
    [InlineData("context.Set<AssistantSettingsAuditRecord>()\n    .ExecuteDelete();")]
    [InlineData("audit.Update(x);")]
    [InlineData("await context.Database.ExecuteSqlRawAsync(\"DELETE FROM assistant_settings_audit\");")]
    [InlineData("var sql = \"update assistant_settings_audit set action = 'x'\";")]
    public void TheScan_DetectsEachKindOfModification(string source)
        => Assert.Single(FindOffendingStatements(source));

    [Theory]
    [InlineData("context.Add(ToAuditRecord(audit));")]
    [InlineData("context.Remove(row);")]
    [InlineData("var sql = \"DELETE FROM assistant_settings\";")]
    public void TheScan_IgnoresInsertionsAndOtherTables(string source)
        => Assert.Empty(FindOffendingStatements(source));

    [Fact]
    public void TheStorePort_OnlyTakesAnAuditEntryInSaveAndDelete_AndExposesNoAuditReader()
    {
        var withAudit = typeof(IAssistantSettingsStore).GetMethods()
            .Where(method => method.GetParameters().Any(parameter => parameter.ParameterType == typeof(AuditEntry)))
            .Select(method => method.Name)
            .Order(StringComparer.Ordinal);

        Assert.Equal(AllowedStoreMembersThatTakeAnAudit, withAudit);
        Assert.DoesNotContain(
            Assembly.Load("TimeTracker.Application").GetTypes(),
            type => type.IsInterface && type.Name.Contains("Audit", StringComparison.Ordinal));
    }

    // Segunda barrera (DD-9): la base de datos rechaza UPDATE, DELETE y TRUNCATE aunque el código futuro lo intente.
    [Fact]
    public void Migrations_InstallTheAppendOnlyTriggersOnTheAuditTable()
    {
        var migrations = string.Join('\n', Directory.EnumerateFiles(Path.Combine(RepositoryRoot.Path, "src", "TimeTracker.Infrastructure", "Persistence", "Migrations"), "*.cs")
            .Where(file => !file.EndsWith(".Designer.cs", StringComparison.Ordinal))
            .Select(File.ReadAllText));

        Assert.Matches(@"CREATE TRIGGER\s+\w+\s+BEFORE UPDATE OR DELETE ON assistant_settings_audit\s+FOR EACH ROW", migrations);
        Assert.Matches(@"CREATE TRIGGER\s+\w+\s+BEFORE TRUNCATE ON assistant_settings_audit\s+FOR EACH STATEMENT", migrations);
    }

    // Un enunciado = texto hasta «;». Una cadena de llamadas partida en líneas sigue siendo un único enunciado.
    private static IEnumerable<string> FindOffendingStatements(string source)
        => source.Split(';')
            .Where(statement => ForbiddenSql().IsMatch(statement)
                || (statement.Contains("audit", StringComparison.OrdinalIgnoreCase) && ForbiddenCall().IsMatch(statement)))
            .Select(statement => statement.Trim());

    private static IEnumerable<string> ProductionSources()
    {
        var separator = Path.DirectorySeparatorChar;
        return Directory.EnumerateFiles(Path.Combine(RepositoryRoot.Path, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{separator}obj{separator}", StringComparison.Ordinal)
                && !file.Contains($"{separator}bin{separator}", StringComparison.Ordinal)
                && !file.Contains($"{separator}Migrations{separator}", StringComparison.Ordinal));
    }
}
