using System.Net;
using System.Text.RegularExpressions;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;

using static TimeTracker.Api.IntegrationTests.Support.RegistrationApiFactory;

namespace TimeTracker.Api.IntegrationTests.Registration;

/// <summary>
/// Privacidad del registro (company-registration, «Privacidad del registro» y «El código no se filtra»): el servicio registra solo
/// metadatos (resultado, instante, identificador de la compañía creada) y ni los registros ni las respuestas contienen la contraseña,
/// los códigos, el correo ni los nombres; tampoco indican cuál de los códigos se aceptó.
/// </summary>
[Collection("Postgres")]
public partial class RegistrationPrivacyTests(PostgresFixture postgres)
{
    private const string Marker = "marcador-privado";

    [Fact]
    public async Task EveryOutcome_LogsOnlyMetadata_AndNothingPersonalOrSecretReachesTheLogsOrTheResponses()
    {
        using var factory = new RegistrationApiFactory(postgres.ConnectionString);
        var client = factory.CreateAnonymousClient();
        var created = Body(companyName: $"Empresa {Marker} {Guid.NewGuid():N}", managerName: $"Persona {Marker}", email: $"{Marker}.{Guid.NewGuid():N}@example.test", password: $"clave-{Marker}-12345");
        var responses = new List<string>();

        responses.Add(await TextAsync(await PostAsync(client, created)));
        responses.Add(await TextAsync(await PostAsync(client, Body(companyName: $"Otra {Marker} {Guid.NewGuid():N}", email: (string)created["email"]!, password: $"clave-{Marker}-12345"))));
        responses.Add(await TextAsync(await PostAsync(client, Body(companyName: (string)created["companyName"]!, password: $"clave-{Marker}-12345"))));
        responses.Add(await TextAsync(await PostAsync(client, Body(companyName: $"Mala {Marker}", managerName: " ", password: $"corta-{Marker}", inviteCode: Code))));
        responses.Add(await TextAsync(await PostAsync(client, Body(companyName: $"Sin código {Marker}", password: $"clave-{Marker}-12345", inviteCode: $"incorrecto-{Marker}-0123456789"))));

        // Los registros no contienen nada de lo enviado (todo lleva el marcador) ni ningún código.
        var logs = string.Join('\n', factory.Logs.Messages);
        foreach (var secret in new[] { Code, SecondCode, Marker, "Gómez", "example.test" })
        {
            Assert.DoesNotContain(secret, logs, StringComparison.Ordinal);
        }

        // Ninguna respuesta repite la contraseña ni un código; las de error tampoco el correo ni los nombres (la del 201 sí lleva a la persona).
        Assert.All(responses, text => Assert.DoesNotContain("clave-", text, StringComparison.Ordinal));
        Assert.All(responses, text => Assert.DoesNotContain(Code, text, StringComparison.Ordinal));
        Assert.All(responses.Skip(1), text => Assert.DoesNotContain(Marker, text, StringComparison.Ordinal));
        var registrations = factory.Logs.Messages.Where(message => message.StartsWith("Registro de compañía:", StringComparison.Ordinal)).ToList();
        Assert.Contains(registrations, message => message.Contains("Created", StringComparison.Ordinal) && GuidPattern().IsMatch(message));
        Assert.Contains(registrations, message => message.Contains("RejectedByDuplicate", StringComparison.Ordinal));
        Assert.Contains(registrations, message => message.Contains("RejectedByValidation", StringComparison.Ordinal));
        Assert.Contains(registrations, message => message.Contains("RejectedByInviteCode", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ARegistrationWithTheSecondCode_LeavesTheSameLogShapeAsTheFirst_SoNoPositionIsRevealed()
    {
        using var factory = new RegistrationApiFactory(postgres.ConnectionString);
        var client = factory.CreateAnonymousClient();

        Assert.Equal(HttpStatusCode.Created, (await PostAsync(client, Body(inviteCode: Code))).StatusCode);
        var afterFirst = RegistrationLines(factory);
        Assert.Equal(HttpStatusCode.Created, (await PostAsync(client, Body(inviteCode: SecondCode))).StatusCode);
        var all = RegistrationLines(factory);

        Assert.Equal(2, all.Count);
        Assert.Single(afterFirst);
        // Mismo texto salvo el identificador de la compañía y el instante: nada indica cuál de los dos códigos se aceptó.
        Assert.Equal(Shape(all[0]), Shape(all[1]));
        Assert.DoesNotMatch(@"(?i)c[oó]digo\s*(n[uú]mero|#|\d)|[ií]ndice|posici[oó]n", string.Join('\n', factory.Logs.Messages));
    }

    [Fact]
    public async Task TheRegistrationLogLine_NamesTheCreatedCompanyById()
    {
        using var factory = new RegistrationApiFactory(postgres.ConnectionString);
        using var json = await ReadAsync(await PostAsync(factory.CreateAnonymousClient(), Body()));
        var companyId = TokenPayload(json.RootElement.GetProperty("accessToken").GetString()!).GetProperty("cid").GetString()!;

        var line = Assert.Single(RegistrationLines(factory));

        Assert.Contains(companyId, line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARejectedAttempt_LogsNoCompanyIdentifier()
    {
        using var factory = new RegistrationApiFactory(postgres.ConnectionString);

        _ = await PostAsync(factory.CreateAnonymousClient(), Body(inviteCode: "incorrecto-0123456789"));

        var line = Assert.Single(RegistrationLines(factory));
        Assert.Contains("RejectedByInviteCode", line, StringComparison.Ordinal);
        Assert.DoesNotMatch(GuidPattern(), line);
    }

    [Fact]
    public async Task AttemptsRejectedByTheRateLimit_AreLoggedAsSuch()
    {
        using var factory = new RegistrationApiFactory(postgres.ConnectionString);
        var client = factory.CreateAnonymousClient();

        for (var attempt = 0; attempt < 6; attempt++)
        {
            _ = await PostAsync(client, Body(inviteCode: "incorrecto-0123456789"));
        }

        var lines = RegistrationLines(factory);
        Assert.Equal(5, lines.Count(line => line.Contains("RejectedByInviteCode", StringComparison.Ordinal)));
        Assert.Single(lines, line => line.Contains("RejectedByRateLimit", StringComparison.Ordinal));
    }

    private static List<string> RegistrationLines(RegistrationApiFactory factory)
        => [.. factory.Logs.Messages.Where(message => message.StartsWith("Registro de compañía:", StringComparison.Ordinal))];

    private static string Shape(string line) => TimestampPattern().Replace(GuidPattern().Replace(line, "<id>"), "<instante>");

    private static async Task<string> TextAsync(HttpResponseMessage response) => await response.Content.ReadAsStringAsync(CancellationToken.None);

    [GeneratedRegex(@"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex GuidPattern();

    [GeneratedRegex(@"\d{4}-\d{2}-\d{2}T[\d:.]+(Z|[+-]\d{2}:\d{2})")]
    private static partial Regex TimestampPattern();
}
