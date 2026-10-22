using System.Net.Http.Headers;

using Microsoft.EntityFrameworkCore;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Domain.Users;

namespace TimeTracker.Api.IntegrationTests.Support;

/// <summary>
/// Dos compañías inventadas, A y B, cada una con un Manager y dos Employees (que son sus reportes directos) y un fichaje por
/// empleado. Es la fábrica común de las pruebas de aislamiento (TC-1 a TC-19): los identificadores de una compañía nunca
/// aparecen en la otra.
/// </summary>
internal sealed class TwoCompanyFixture
{
    private TwoCompanyFixture(string connectionString, CompanyMembers a, CompanyMembers b)
    {
        ConnectionString = connectionString;
        A = a;
        B = b;
    }

    public string ConnectionString { get; }

    public CompanyMembers A { get; }

    public CompanyMembers B { get; }

    /// <summary>Crea las dos compañías. Con <paramref name="freshDatabase"/> usa una base migrada propia; si no, la compartida (nombres únicos).</summary>
    public static async Task<TwoCompanyFixture> CreateAsync(PostgresFixture postgres, bool freshDatabase = false)
    {
        var connectionString = postgres.ConnectionString;
        if (freshDatabase)
        {
            connectionString = await postgres.CreateEmptyDatabaseAsync();
            await using var context = postgres.CreateContext(connectionString);
            await context.Database.MigrateAsync(CancellationToken.None);
        }

        var a = await CreateCompanyAsync(connectionString, "A");
        var b = await CreateCompanyAsync(connectionString, "B");
        return new TwoCompanyFixture(connectionString, a, b);
    }

    /// <summary>Cliente HTTP con el token de la persona (el claim <c>cid</c> es el de su compañía).</summary>
    public static HttpClient ClientFor(ApiFactory factory, CompanyMembers company, Member member)
    {
        var client = factory.CreateDefaultClient();
        var token = TestTokens.Create(member.Id, member.Role.ToString(), factory.Clock.GetUtcNow(), companyId: company.Id.ToString(), companyName: company.Name);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<CompanyMembers> CreateCompanyAsync(string connectionString, string label)
    {
        var companyId = await CompanySeed.InsertAsync(connectionString, $"compania {label} {Guid.NewGuid():N}");
        var manager = NewMember(label, "manager", UserRole.Manager);
        var first = NewMember(label, "empleado1", UserRole.Employee);
        var second = NewMember(label, "empleado2", UserRole.Employee);
        await UserSeed.InsertRawAsync(connectionString, manager.Id, manager.Email, "Manager", companyId: companyId);
        foreach (var employee in new[] { first, second })
        {
            await UserSeed.InsertRawAsync(connectionString, employee.Id, employee.Email, "Employee", manager.Id, companyId: companyId);
        }

        foreach (var member in new[] { manager, first, second })
        {
            await CompanySeed.InsertTimeEntryAsync(connectionString, companyId, member.Id);
        }

        return new CompanyMembers(companyId, $"compania {label}", manager, first, second);
    }

    private static Member NewMember(string label, string name, UserRole role)
    {
        var id = Guid.NewGuid();
        return new Member(id, $"{name}.{label.ToLowerInvariant()}.{id:N}@example.test", role);
    }

    internal sealed record Member(Guid Id, string Email, UserRole Role);

    internal sealed record CompanyMembers(Guid Id, string Name, Member Manager, Member Employee1, Member Employee2)
    {
        public IEnumerable<Member> All => [Manager, Employee1, Employee2];
    }
}
