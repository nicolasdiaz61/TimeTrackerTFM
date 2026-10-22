using System.Net;
using System.Security.Claims;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Api.Security;
using TimeTracker.Application.Identity;
using TimeTracker.Domain.Users;
using TimeTracker.Infrastructure.Persistence;

using static TimeTracker.Api.IntegrationTests.Support.RegistrationApiFactory;

namespace TimeTracker.Api.IntegrationTests.Registration;

/// <summary>
/// Registro con un token vigente (company-registration, «Registro con un token vigente»): el endpoint es anónimo e ignora el token,
/// el contexto de la petición nunca adopta la compañía del token y la compañía nueva se crea en su propio ámbito.
/// </summary>
[Collection("Postgres")]
public class RegisterWithTokenTests(PostgresFixture postgres)
{
    [Fact]
    public async Task ARegistrationWithAValidManagerToken_CreatesANewCompany_AndLeavesTheCompanyOfTheTokenUntouched()
    {
        var fixture = await TwoCompanyFixture.CreateAsync(postgres, freshDatabase: true);
        var companySetter = new SetterRecorder();
        using var baseFactory = new RegistrationApiFactory(fixture.ConnectionString);
        using var factory = baseFactory.WithWebHostBuilder(companySetter.InstallOn);
        var token = TestTokens.Create(fixture.A.Manager.Id, "Manager", baseFactory.Clock.GetUtcNow(), companyId: fixture.A.Id.ToString(), companyName: fixture.A.Name);
        var client = factory.CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var aBefore = await SnapshotAsync(fixture.ConnectionString, fixture.A.Id);
        var body = Body();

        var response = await PostAsync(client, body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = await ReadAsync(response);
        var newCompany = Guid.Parse(TokenPayload(json.RootElement.GetProperty("accessToken").GetString()!).GetProperty("cid").GetString()!);
        Assert.NotEqual(fixture.A.Id, newCompany);
        Assert.NotEqual(fixture.B.Id, newCompany);
        Assert.Equal(aBefore, await SnapshotAsync(fixture.ConnectionString, fixture.A.Id));
        Assert.Equal(["1"], await QueryAsync(fixture.ConnectionString, $"SELECT count(*)::text FROM users WHERE company_id = '{newCompany}' AND email = '{(string)body["email"]!}'"));
        // El contexto de la petición no adoptó la compañía del token: la única compañía fijada fue la de la nueva, en su propio ámbito.
        Assert.Equal([newCompany], companySetter.Calls);
    }

    [Fact]
    public async Task WithoutAToken_TheOnlyCompanyEverSetIsTheNewOne()
    {
        var connectionString = postgres.ConnectionString;
        var companySetter = new SetterRecorder();
        using var factory = new RegistrationApiFactory(connectionString).WithWebHostBuilder(companySetter.InstallOn);

        using var json = await ReadAsync(await PostAsync(factory.CreateAnonymousClient(), Body()));

        var newCompany = Guid.Parse(TokenPayload(json.RootElement.GetProperty("accessToken").GetString()!).GetProperty("cid").GetString()!);
        Assert.Equal([newCompany], companySetter.Calls);
    }

    [Fact]
    public void OnlyTheRegistrationEndpointCarriesTheSkipCompanyContextMarker()
    {
        using var factory = new RegistrationApiFactory(postgres.ConnectionString);
        _ = factory.CreateClient();

        var marked = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<SkipCompanyContextMetadata>() is not null)
            .SelectMany(endpoint => endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Select(method => $"{method} {endpoint.RoutePattern.RawText}"))
            .ToList();

        Assert.Equal(["POST /api/auth/register-company"], marked);
    }

    [Fact]
    public async Task TheMiddleware_DoesNotSetTheCompanyOfAnAuthenticatedUserOnAnEndpointMarkedToSkipIt()
    {
        var ambient = new AmbientCompanyContext();
        var continued = false;
        var middleware = new CompanyContextMiddleware(_ =>
        {
            continued = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity("test")) };
        context.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(new SkipCompanyContextMetadata()), "registro"));

        await middleware.InvokeAsync(context, ambient, new ThrowingCurrentUser());

        Assert.Null(ambient.CompanyId);
        Assert.True(continued);
    }

    [Fact]
    public async Task TheMiddleware_StillSetsTheCompanyOnAnyOtherEndpoint()
    {
        var ambient = new AmbientCompanyContext();
        var companyId = Guid.NewGuid();
        var middleware = new CompanyContextMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity("test")) };
        context.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(), "otro"));

        await middleware.InvokeAsync(context, ambient, new FixedCurrentUser(companyId));

        Assert.Equal(companyId, ambient.CompanyId);
    }

    private static async Task<List<string>> SnapshotAsync(string connectionString, Guid companyId)
        => await QueryAsync(
            connectionString,
            $"SELECT 'c|' || id || '|' || name FROM companies WHERE id = '{companyId}' " +
            $"UNION ALL SELECT 'u|' || id || '|' || email || '|' || password_hash FROM users WHERE company_id = '{companyId}' " +
            $"UNION ALL SELECT 't|' || id || '|' || clock_in_at FROM time_entries WHERE company_id = '{companyId}' ORDER BY 1");

    private static async Task<List<string>> QueryAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(CancellationToken.None);
        var rows = new List<string>();
        while (await reader.ReadAsync(CancellationToken.None))
        {
            rows.Add(reader.GetString(0));
        }

        return rows;
    }

    /// <summary>Anota cada compañía que se fija en cualquier ámbito (el de la petición o uno creado con <c>CreateScope</c>) y delega en el contexto real.</summary>
    private sealed class SetterRecorder
    {
        private readonly List<Guid> _calls = [];

        public IReadOnlyList<Guid> Calls
        {
            get
            {
                lock (_calls)
                {
                    return [.. _calls];
                }
            }
        }

        public void InstallOn(IWebHostBuilder builder)
            => builder.ConfigureTestServices(services =>
                services.AddScoped<ICompanyContextSetter>(provider => new RecordingSetter(provider.GetRequiredService<AmbientCompanyContext>(), _calls)));
    }

    private sealed class RecordingSetter(AmbientCompanyContext inner, List<Guid> calls) : ICompanyContextSetter
    {
        public void Set(Guid companyId)
        {
            lock (calls)
            {
                calls.Add(companyId);
            }

            inner.Set(companyId);
        }
    }

    private sealed class ThrowingCurrentUser : ICurrentUser
    {
        public Guid EmployeeId => throw new InvalidOperationException("No se debe leer la identidad.");

        public Guid CompanyId => throw new InvalidOperationException("No se debe leer la compañía del token.");

        public UserRole Role => throw new InvalidOperationException("No se debe leer el rol.");
    }

    private sealed class FixedCurrentUser(Guid companyId) : ICurrentUser
    {
        public Guid EmployeeId => Guid.NewGuid();

        public Guid CompanyId => companyId;

        public UserRole Role => UserRole.Manager;
    }
}
