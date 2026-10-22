using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Identity;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;

using static TimeTracker.Api.IntegrationTests.Support.EmployeesHttp;

namespace TimeTracker.Api.IntegrationTests.Authentication;

/// <summary>
/// Almacenamiento y verificación de contraseñas y política de respaldo autenticada (authentication, tarea 6.15b), recorriendo registro,
/// alta, regeneración, inicio de sesión y cambio de contraseña. Valores inventados; la única contraseña que sale en una respuesta es
/// <c>temporaryPassword</c>, en las del alta y la regeneración.
/// </summary>
[Collection("Postgres")]
public class PasswordStorageAndFallbackPolicyTests(PostgresFixture postgres)
{
    private const string PlainPassword = "clave-de-prueba-123";

    // Campos que solo pueden declarar peticiones o las dos respuestas con contraseña temporal; «mustChangePassword» es un indicador, no una contraseña.
    private static readonly string[] PasswordFields = ["password", "temporaryPassword", "currentPassword", "newPassword", "passwordHash"];

    [Fact]
    public void TheHash_DoesNotContainThePassword_Verifies_AndUsesARandomSalt()
    {
        var hasher = new TimeTracker.Api.Authentication.AspNetPasswordHasher();

        var first = hasher.Hash(PlainPassword);
        var second = hasher.Hash(PlainPassword);

        Assert.DoesNotContain(PlainPassword, first, StringComparison.Ordinal);
        Assert.True(hasher.Verify(first, PlainPassword));
        Assert.True(hasher.Verify(second, PlainPassword));
        Assert.NotEqual(first, second);
        Assert.False(hasher.Verify(first, "otra-clave-de-prueba-456"));
        Assert.NotEqual(PasswordVerificationResult.Failed, new PasswordHasher<object>().VerifyHashedPassword(new object(), first, PlainPassword));
    }

    [Fact]
    public async Task AcrossTheWholeJourney_OnlyTheCreationAndTheRegenerationReturnATemporaryPassword_AndNoOtherPasswordIsEchoed()
    {
        const string ManagerPassword = "contrasena-prueba-12345";
        const string ChosenPassword = "elegida-por-la-persona-678";
        using var factory = new RegistrationApiFactory(postgres.ConnectionString);
        var anonymous = factory.CreateAnonymousClient();
        var managerEmail = $"ana.{Guid.NewGuid():N}@example.test";
        var personEmail = $"luis.{Guid.NewGuid():N}@example.test";
        var bodies = new List<(string Step, string Text)>();

        var registered = await RegistrationApiFactory.PostAsync(anonymous, RegistrationApiFactory.Body(email: managerEmail, password: ManagerPassword));
        bodies.Add(("registro", await registered.Content.ReadAsStringAsync(CancellationToken.None)));
        using var registration = JsonDocument.Parse(bodies[^1].Text);
        var manager = WithToken(factory, registration.RootElement.GetProperty("accessToken").GetString()!);

        var created = await CreateAsync(manager, new { displayName = "Luis", email = personEmail });
        var createdText = await created.Content.ReadAsStringAsync(CancellationToken.None);
        using var createdBody = JsonDocument.Parse(createdText);
        var firstTemporary = createdBody.RootElement.GetProperty("temporaryPassword").GetString()!;
        var personId = createdBody.RootElement.GetProperty("id").GetGuid();

        var regenerated = await RegenerateAsync(manager, personId);
        var regeneratedText = await regenerated.Content.ReadAsStringAsync(CancellationToken.None);
        using var regeneratedBody = JsonDocument.Parse(regeneratedText);
        var secondTemporary = regeneratedBody.RootElement.GetProperty("temporaryPassword").GetString()!;

        var login = await LoginAsync(factory, personEmail, secondTemporary);
        bodies.Add(("login", await login.Content.ReadAsStringAsync(CancellationToken.None)));
        using var session = JsonDocument.Parse(bodies[^1].Text);
        var change = await ChangePasswordAsync(WithToken(factory, session.RootElement.GetProperty("accessToken").GetString()!), secondTemporary, ChosenPassword);
        bodies.Add(("cambio", await change.Content.ReadAsStringAsync(CancellationToken.None)));
        bodies.Add(("equipo", await manager.GetStringAsync(TeamMembersPath, CancellationToken.None)));
        bodies.Add(("yo", await manager.GetStringAsync("/api/auth/me", CancellationToken.None)));

        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, regenerated.StatusCode);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);
        Assert.NotEqual(firstTemporary, secondTemporary);
        // Ninguna otra respuesta repite una contraseña ni declara un campo de contraseña o de hash.
        Assert.NotEmpty(bodies);
        foreach (var (step, text) in bodies)
        {
            foreach (var secret in new[] { ManagerPassword, ChosenPassword, firstTemporary, secondTemporary })
            {
                Assert.False(text.Contains(secret, StringComparison.Ordinal), $"La respuesta de «{step}» contiene una contraseña.");
            }

            using var parsed = JsonDocument.Parse(text);
            Assert.DoesNotContain(PropertyNames(parsed.RootElement), name => PasswordFields.Contains(name, StringComparer.OrdinalIgnoreCase));
        }

        Assert.Contains("temporaryPassword", createdText, StringComparison.Ordinal);
        Assert.Contains("temporaryPassword", regeneratedText, StringComparison.Ordinal);
        var logs = string.Join('\n', factory.Logs.Messages);
        foreach (var secret in new[] { ManagerPassword, ChosenPassword, firstTemporary, secondTemporary })
        {
            Assert.DoesNotContain(secret, logs, StringComparison.Ordinal);
        }
    }

    private static IEnumerable<string> PropertyNames(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().SelectMany(property => PropertyNames(property.Value).Prepend(property.Name)),
        JsonValueKind.Array => element.EnumerateArray().SelectMany(PropertyNames),
        _ => [],
    };

    [Theory]
    [InlineData("GET", "/api/auth/me")]
    [InlineData("POST", "/api/auth/change-password")]
    [InlineData("POST", "/api/employees")]
    [InlineData("POST", "/api/employees/5b1f0c1e-7a44-4f0e-9c52-3d8a6e2b9f10/temporary-password")]
    [InlineData("POST", "/api/time-entries/clock-in")]
    [InlineData("POST", "/api/time-entries/clock-out")]
    [InlineData("GET", "/api/time-entries")]
    [InlineData("GET", "/api/team/members")]
    [InlineData("GET", "/api/team/members/5b1f0c1e-7a44-4f0e-9c52-3d8a6e2b9f10/time-entries")]
    public async Task EveryProtectedEndpoint_WithoutAToken_Returns401_WhateverTheContentType(string method, string path)
    {
        using var factory = new ApiFactory();
        var client = factory.CreateAnonymousClient();

        var plain = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path), CancellationToken.None);
        var notJson = await client.SendAsync(
            new HttpRequestMessage(new HttpMethod(method), path) { Content = new StringContent("texto", Encoding.UTF8, "text/plain") }, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, plain.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, notJson.StatusCode);
    }

    [Fact]
    public async Task AnAuthenticatedManager_WithANonJsonBody_ReachesTheEndpoint_AndGetsItsOwnError()
    {
        // Control del caso anterior: el 401 se debe a la falta de token, no a que el tipo de contenido impida llegar al endpoint.
        var fixture = await TwoCompanyFixture.CreateAsync(postgres);
        using var factory = new EmployeesApiFactory(fixture.ConnectionString);
        var manager = TwoCompanyFixture.ClientFor(factory, fixture.A, fixture.A.Manager);

        var response = await manager.PostAsync(EmployeesPath, new StringContent("texto", Encoding.UTF8, "text/plain"), CancellationToken.None);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }
}
