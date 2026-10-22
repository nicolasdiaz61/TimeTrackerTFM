using System.Net;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;

using static TimeTracker.Api.IntegrationTests.Support.EmployeesHttp;

namespace TimeTracker.Api.IntegrationTests.Employees;

/// <summary>
/// Tope de personas por compañía (employee-management): <c>Company:MaxEmployees</c> exacto, también con altas simultáneas, y propio
/// de cada compañía. La carrera real se ejercita con cinco peticiones a la vez contra PostgreSQL (V-14).
/// </summary>
[Collection("Postgres")]
public class EmployeeCapTests(PostgresFixture postgres)
{
    private const string CapDetail = "Se ha alcanzado el número máximo de personas de la compañía.";
    private const string DuplicateDetail = "Ya existe una cuenta con ese correo electrónico.";

    private static Dictionary<string, string?> Cap(int value) => new() { ["Company:MaxEmployees"] = value.ToString(System.Globalization.CultureInfo.InvariantCulture) };

    private static string NewEmail() => $"alta.{Guid.NewGuid():N}@example.test";

    private static HttpClient ManagerClient(EmployeesApiFactory factory, Guid companyId, Guid managerId)
        => WithToken(factory, TestTokens.Create(managerId, "Manager", factory.Clock.GetUtcNow(), companyId: companyId.ToString(), companyName: "compania de prueba"));

    // Compañía con un Manager y tantos Employees como se pidan; devuelve el cliente del Manager.
    private async Task<(Guid CompanyId, HttpClient Manager)> CompanyWithAsync(EmployeesApiFactory factory, string connectionString, int users)
    {
        var companyId = await CompanySeed.InsertAsync(connectionString);
        var managerId = Guid.NewGuid();
        await UserSeed.InsertRawAsync(connectionString, managerId, NewEmail(), "Manager", companyId: companyId);
        for (var index = 1; index < users; index++)
        {
            await UserSeed.InsertRawAsync(connectionString, Guid.NewGuid(), NewEmail(), managerId: managerId, companyId: companyId);
        }

        return (companyId, ManagerClient(factory, companyId, managerId));
    }

    [Fact]
    public async Task AtTheCap_TheCreationGives409WithTheCapDetail_DistinctFromTheDuplicateEmailOne()
    {
        var connectionString = postgres.ConnectionString;
        using var factory = new EmployeesApiFactory(connectionString, Cap(3));
        var (companyId, manager) = await CompanyWithAsync(factory, connectionString, users: 3);
        var existing = Assert.Single(await MigrationDatabase.QueryAsync(connectionString, $"SELECT email FROM users WHERE company_id = '{companyId}' AND role = 'Employee' LIMIT 1"));

        var atTheCap = await CreateAsync(manager, new { displayName = "Luis", email = NewEmail() });
        var duplicateAtTheCap = await CreateAsync(manager, new { displayName = "Luis", email = existing });

        Assert.Equal(HttpStatusCode.Conflict, atTheCap.StatusCode);
        Assert.Equal("application/problem+json", atTheCap.Content.Headers.ContentType?.MediaType);
        using var body = await ReadAsync(atTheCap);
        Assert.Equal(CapDetail, body.RootElement.GetProperty("detail").GetString());
        Assert.NotEqual(DuplicateDetail, CapDetail);
        Assert.Equal(3, await CountUsersAsync(connectionString, companyId));
        // Con el tope alcanzado, un correo repetido ya no llega a crear nada: sigue siendo 409.
        Assert.Equal(HttpStatusCode.Conflict, duplicateAtTheCap.StatusCode);
    }

    [Fact]
    public async Task OneBelowTheCap_TheCreationSucceeds_AndTheCountReachesTheCap()
    {
        var connectionString = postgres.ConnectionString;
        using var factory = new EmployeesApiFactory(connectionString, Cap(3));
        var (companyId, manager) = await CompanyWithAsync(factory, connectionString, users: 2);

        var created = await CreateAsync(manager, new { displayName = "Luis", email = NewEmail() });
        var beyond = await CreateAsync(manager, new { displayName = "Ana", email = NewEmail() });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, beyond.StatusCode);
        Assert.Equal(3, await CountUsersAsync(connectionString, companyId));
    }

    [Fact]
    public async Task FiveSimultaneousCreationsAtTheLastSlot_GiveExactlyOne201AndFour409_WithoutA500()
    {
        var connectionString = postgres.ConnectionString;
        using var factory = new EmployeesApiFactory(connectionString, Cap(3));
        for (var round = 0; round < 3; round++)
        {
            var (companyId, manager) = await CompanyWithAsync(factory, connectionString, users: 2);
            // Los cinco correos son distintos: solo el tope puede rechazar a cuatro de ellos.
            var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(index => CreateAsync(manager, new { displayName = $"Persona {index}", email = NewEmail() })));

            var statuses = responses.Select(response => response.StatusCode).ToList();
            Assert.True(statuses.Count(status => status == HttpStatusCode.Created) == 1, string.Join(",", statuses));
            var errors = string.Join(Environment.NewLine, factory.Logs.Messages.Where(message => message.Contains("nhandled", StringComparison.Ordinal)).Take(1));
            Assert.True(statuses.Count(status => status == HttpStatusCode.Conflict) == 4, $"{string.Join(",", statuses)}{Environment.NewLine}{errors}");
            Assert.DoesNotContain(statuses, status => (int)status >= 500);
            Assert.Equal(3, await CountUsersAsync(connectionString, companyId));
        }
    }

    [Fact]
    public async Task TheCapOfOneCompany_DoesNotAffectAnother()
    {
        var connectionString = postgres.ConnectionString;
        using var factory = new EmployeesApiFactory(connectionString, Cap(3));
        var (fullId, full) = await CompanyWithAsync(factory, connectionString, users: 3);
        var (roomyId, roomy) = await CompanyWithAsync(factory, connectionString, users: 1);

        var blocked = await CreateAsync(full, new { displayName = "Luis", email = NewEmail() });
        var allowed = await CreateAsync(roomy, new { displayName = "Luis", email = NewEmail() });

        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
        Assert.Equal(3, await CountUsersAsync(connectionString, fullId));
        Assert.Equal(2, await CountUsersAsync(connectionString, roomyId));
    }

    [Fact]
    public async Task TheDefaultCapIs50()
    {
        var connectionString = postgres.ConnectionString;
        using var factory = new EmployeesApiFactory(connectionString);
        var (companyId, manager) = await CompanyWithAsync(factory, connectionString, users: 49);

        var fiftieth = await CreateAsync(manager, new { displayName = "Luis", email = NewEmail() });
        var fiftyFirst = await CreateAsync(manager, new { displayName = "Ana", email = NewEmail() });

        Assert.Equal(HttpStatusCode.Created, fiftieth.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, fiftyFirst.StatusCode);
        Assert.Equal(50, await CountUsersAsync(connectionString, companyId));
    }
}
