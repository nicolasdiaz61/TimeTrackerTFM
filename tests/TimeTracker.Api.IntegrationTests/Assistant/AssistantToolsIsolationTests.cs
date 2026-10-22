using System.Data.Common;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Application.Team;
using TimeTracker.Application.Tests.Fakes;
using TimeTracker.Application.TimeTracking;
using TimeTracker.Domain.Users;
using TimeTracker.Infrastructure.Assistant;
using TimeTracker.Infrastructure.Persistence;

namespace TimeTracker.Api.IntegrationTests.Assistant;

/// <summary>
/// Herramientas del asistente acotadas a la compañía del token (TC-8). Con dos compañías en PostgreSQL real, las tres herramientas
/// se ejecutan como el modelo las invocaría (también con un GUID de la otra compañía inyectado y argumentos extra): nada de B
/// llega al resultado ni al registro, y un miembro de B es indistinguible de un GUID inexistente. El chat de B entero se prueba en
/// <c>AssistantRuntimeIsolationTests</c>; aquí las herramientas se ejercitan sobre el mismo repositorio filtrado que usa la API.
/// </summary>
[Collection("Postgres")]
public sealed class AssistantToolsIsolationTests(PostgresFixture postgres)
{
    private const string NotFound = "No hay ningún miembro de tu equipo con ese identificador.";

    private static readonly DateTimeOffset Now = new(2026, 10, 18, 10, 0, 0, TimeSpan.Zero);

    private sealed class Rig(AssistantToolset toolset, CapturingLoggerProvider logs, QueryRecorder queries)
    {
        public CapturingLoggerProvider Logs { get; } = logs;

        public QueryRecorder Queries { get; } = queries;

        public IReadOnlyList<AIFunction> Tools { get; } = toolset.CreateTools();

        public async Task<string> InvokeAsync(string tool, params (string Name, object? Value)[] arguments)
        {
            var function = Tools.Single(candidate => candidate.Name == tool);
            var args = new AIFunctionArguments();
            foreach (var (name, value) in arguments)
            {
                args[name] = value;
            }

            return JsonSerializer.SerializeToElement(await function.InvokeAsync(args), function.JsonSerializerOptions).GetRawText();
        }
    }

    private sealed class QueryRecorder : DbCommandInterceptor
    {
        public List<string> Sql { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Sql.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private static Rig RigFor(string connectionString, Guid companyId, Guid userId, UserRole role)
    {
        var queries = new QueryRecorder();
        var company = new TestCompanyContext(companyId);
        var context = new TimeTrackerDbContext(
            new DbContextOptionsBuilder<TimeTrackerDbContext>().UseNpgsql(connectionString).AddInterceptors(queries).Options, company);
        var currentUser = new FixedCurrentUser(userId, role, companyId);
        var users = new UserRepository(context);
        var entries = new TimeEntryRepository(context);
        var logs = new CapturingLoggerProvider();
        var toolset = new AssistantToolset(
            new GetTimeEntriesUseCase(entries, currentUser),
            new GetTeamMembersUseCase(users, currentUser),
            new GetTeamMemberTimeEntriesUseCase(users, entries, currentUser),
            currentUser,
            new FakeTimeProvider(Now),
            LoggerFactory.Create(builder => builder.AddProvider(logs)).CreateLogger<AssistantToolset>());
        return new Rig(toolset, logs, queries);
    }

    private async Task<TwoCompanyFixture> FixtureAsync()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        // Nombres reconocibles: así se detecta cualquier nombre de la otra compañía.
        await MigrationDatabase.ExecuteAsync(fixture.ConnectionString, $"UPDATE users SET display_name = 'Persona de A' WHERE company_id = '{fixture.A.Id}'");
        await MigrationDatabase.ExecuteAsync(fixture.ConnectionString, $"UPDATE users SET display_name = 'Persona de B' WHERE company_id = '{fixture.B.Id}'");
        return fixture;
    }

    [Fact]
    public async Task TheToolSchemas_HaveNoCompanyParameter()
    {
        var fixture = await FixtureAsync();
        var rig = RigFor(fixture.ConnectionString, fixture.A.Id, fixture.A.Manager.Id, UserRole.Manager);

        Assert.Equal(3, rig.Tools.Count);
        var parameters = rig.Tools.SelectMany(tool => tool.JsonSchema.GetProperty("properties").EnumerateObject().Select(property => property.Name)).Distinct().Order().ToList();
        Assert.Equal(["fromDate", "memberId", "toDate"], parameters);
    }

    [Fact]
    public async Task GetTeamMembers_ListsOnlyTheDirectReportsOfTheCompanyOfTheToken()
    {
        var fixture = await FixtureAsync();
        var rig = RigFor(fixture.ConnectionString, fixture.A.Id, fixture.A.Manager.Id, UserRole.Manager);

        var result = await rig.InvokeAsync("get_team_members");

        Assert.Contains(fixture.A.Employee1.Id.ToString(), result, StringComparison.Ordinal);
        Assert.Contains(fixture.A.Employee2.Id.ToString(), result, StringComparison.Ordinal);
        Assert.Contains("Persona de A", result, StringComparison.Ordinal);
        Assert.DoesNotContain("Persona de B", result, StringComparison.Ordinal);
        foreach (var other in fixture.B.All)
        {
            Assert.DoesNotContain(other.Id.ToString(), result, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task GetMyTimeEntries_ReturnsOnlyTheHoursOfThatEmployee()
    {
        var fixture = await FixtureAsync();
        var rig = RigFor(fixture.ConnectionString, fixture.A.Id, fixture.A.Employee1.Id, UserRole.Employee);

        var result = await rig.InvokeAsync("get_my_time_entries", ("fromDate", "2026-10-12"), ("toDate", "2026-10-18"));

        // El fichaje sembrado dura una hora y cada persona de A y de B tiene uno el mismo día: solo cuenta el propio.
        using var document = JsonDocument.Parse(result);
        Assert.Equal(60, document.RootElement.GetProperty("workedMinutes").GetInt32());
    }

    [Fact]
    public async Task AMemberOfAnotherCompany_IsIndistinguishableFromANonexistentGuid_AndNoTimeEntriesAreQueried()
    {
        var fixture = await FixtureAsync();
        var dates = (("fromDate", (object?)"2026-10-12"), ("toDate", (object?)"2026-10-18"));
        var foreign = RigFor(fixture.ConnectionString, fixture.A.Id, fixture.A.Manager.Id, UserRole.Manager);
        var missing = RigFor(fixture.ConnectionString, fixture.A.Id, fixture.A.Manager.Id, UserRole.Manager);
        var own = RigFor(fixture.ConnectionString, fixture.A.Id, fixture.A.Manager.Id, UserRole.Manager);

        var ofB = await foreign.InvokeAsync("get_team_member_time_entries", ("memberId", fixture.B.Employee1.Id.ToString()), dates.Item1, dates.Item2);
        var nonexistent = await missing.InvokeAsync("get_team_member_time_entries", ("memberId", Guid.NewGuid().ToString()), dates.Item1, dates.Item2);
        var ownMember = await own.InvokeAsync("get_team_member_time_entries", ("memberId", fixture.A.Employee1.Id.ToString()), dates.Item1, dates.Item2);

        Assert.Equal(nonexistent, ofB);
        Assert.Contains(NotFound, ofB, StringComparison.Ordinal);
        Assert.DoesNotContain("Persona de B", ofB, StringComparison.Ordinal);
        Assert.DoesNotContain(foreign.Queries.Sql, sql => sql.Contains("time_entries", StringComparison.Ordinal));
        // Control positivo: un miembro propio sí consulta fichajes y devuelve sus horas.
        Assert.Contains(own.Queries.Sql, sql => sql.Contains("time_entries", StringComparison.Ordinal));
        Assert.Contains("\"workedMinutes\":60", ownMember, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExtraArgumentsWithTheCompanyOfBAndAMemberOfB_AreIgnored_AndTheResultHasOnlyDataOfA()
    {
        var fixture = await FixtureAsync();
        var rig = RigFor(fixture.ConnectionString, fixture.A.Id, fixture.A.Manager.Id, UserRole.Manager);

        var members = await rig.InvokeAsync("get_team_members", ("companyId", fixture.B.Id.ToString()), ("memberId", fixture.B.Employee1.Id.ToString()));
        var mine = await rig.InvokeAsync("get_my_time_entries", ("fromDate", "2026-10-12"), ("toDate", "2026-10-18"), ("companyId", fixture.B.Id.ToString()), ("memberId", fixture.B.Employee1.Id.ToString()));
        var member = await rig.InvokeAsync("get_team_member_time_entries", ("memberId", fixture.B.Employee1.Id.ToString()), ("fromDate", "2026-10-12"), ("toDate", "2026-10-18"), ("companyId", fixture.B.Id.ToString()));

        Assert.Contains(fixture.A.Employee1.Id.ToString(), members, StringComparison.Ordinal);
        Assert.DoesNotContain("Persona de B", members + mine + member, StringComparison.Ordinal);
        Assert.Contains("\"workedMinutes\":60", mine, StringComparison.Ordinal);
        Assert.Contains(NotFound, member, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidDates_AreReportedBeforeAMemberOfAnotherCompanyIsLookedUp()
    {
        var fixture = await FixtureAsync();
        var rig = RigFor(fixture.ConnectionString, fixture.A.Id, fixture.A.Manager.Id, UserRole.Manager);

        var result = await rig.InvokeAsync("get_team_member_time_entries", ("memberId", fixture.B.Employee1.Id.ToString()), ("fromDate", "2026-10-18"), ("toDate", "2026-10-12"));

        Assert.DoesNotContain(NotFound, result, StringComparison.Ordinal);
        Assert.Contains("error", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheToolAuditLog_NeverContainsTheRequestedMemberIdOrNamesOfAnotherCompany()
    {
        var fixture = await FixtureAsync();
        var rig = RigFor(fixture.ConnectionString, fixture.A.Id, fixture.A.Manager.Id, UserRole.Manager);

        await rig.InvokeAsync("get_team_member_time_entries", ("memberId", fixture.B.Employee1.Id.ToString()), ("fromDate", "2026-10-12"), ("toDate", "2026-10-18"));

        var entry = Assert.Single(rig.Logs.Messages);
        Assert.Contains("get_team_member_time_entries", entry, StringComparison.Ordinal);
        Assert.Contains(fixture.A.Manager.Id.ToString(), entry, StringComparison.Ordinal);
        Assert.Contains("not-found", entry, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.B.Employee1.Id.ToString(), entry, StringComparison.Ordinal);
        Assert.DoesNotContain("Persona de B", entry, StringComparison.Ordinal);
    }
}
