using System.Net;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Npgsql;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Application.Authentication;
using TimeTracker.Domain.Users;

using static TimeTracker.Api.IntegrationTests.Support.RegistrationApiFactory;

namespace TimeTracker.Api.IntegrationTests.Registration;

/// <summary>
/// Si la sesión no se puede emitir tras crear la compañía y el Manager, la respuesta no es un 500 que invite a reintentar (y a
/// duplicar): es un resultado claro que dice que la cuenta existe y manda a iniciar sesión (design.md, desviaciones de la PR 5.1).
/// </summary>
[Collection("Postgres")]
public class RegistrationSessionFailureTests(PostgresFixture postgres)
{
    private sealed class FailingIssuer : IAccessTokenIssuer
    {
        public AccessToken Issue(UserAccount user, string companyName) => throw new InvalidOperationException("fallo simulado al firmar");
    }

    [Fact]
    public async Task WhenTheSessionCannotBeIssued_TheAccountExists_AndTheAnswerIsClearInsteadOfA500()
    {
        using var baseFactory = new RegistrationApiFactory(postgres.ConnectionString);
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Singleton<IAccessTokenIssuer, FailingIssuer>())));
        var client = factory.CreateAnonymousClient();
        var body = Body();

        var response = await PostAsync(client, body);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var problem = await ReadAsync(response);
        Assert.Equal(
            "La compañía y la cuenta se han creado, pero no se ha podido iniciar la sesión. No repita el registro: inicie sesión con su correo electrónico y su contraseña.",
            problem.RootElement.GetProperty("detail").GetString());
        Assert.False(problem.RootElement.TryGetProperty("accessToken", out _));
        Assert.Equal(1L, await CountAsync($"SELECT count(*) FROM users WHERE email = '{((string)body["email"]!).ToLowerInvariant()}'"));

        var retry = await PostAsync(client, body);
        Assert.Equal(HttpStatusCode.Conflict, retry.StatusCode);
        using var retryProblem = await ReadAsync(retry);
        Assert.NotEqual(problem.RootElement.GetProperty("detail").GetString(), retryProblem.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task WhenTheSessionCannotBeIssued_OnlyTheResultAndTheInstantAreLogged()
    {
        using var baseFactory = new RegistrationApiFactory(postgres.ConnectionString);
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Singleton<IAccessTokenIssuer, FailingIssuer>())));
        var body = Body();

        _ = await PostAsync(factory.CreateAnonymousClient(), body);

        var logs = baseFactory.Logs.Messages.Where(message => message.StartsWith("Registro de compañía:", StringComparison.Ordinal)).ToList();
        Assert.Single(logs);
        Assert.Contains("CreatedWithoutSession", logs[0], StringComparison.Ordinal);
        Assert.DoesNotContain((string)body["email"]!, string.Join('\n', baseFactory.Logs.Messages), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Password, string.Join('\n', baseFactory.Logs.Messages), StringComparison.Ordinal);
    }

    private async Task<long> CountAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand(sql, connection);
        return (long)(await command.ExecuteScalarAsync(CancellationToken.None))!;
    }
}
