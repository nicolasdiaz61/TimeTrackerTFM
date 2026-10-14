using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

using TimeTracker.Api.IntegrationTests.Authentication;
using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Api.IntegrationTests.TimeEntries;

namespace TimeTracker.Api.IntegrationTests.Security;

[Collection("Postgres")]
public class SigningKeyLeakTests(PostgresFixture postgres)
{
    private const string RecognizableKey = "clave-reconocible-que-no-debe-filtrarse-9f3a1c";

    [Fact]
    public async Task SigningKey_DoesNotAppearInResponsesOrLogs()
    {
        var logs = new CapturingLoggerProvider();
        var user = UserSeed.Create("luis");
        await using (var context = postgres.CreateContext(postgres.ConnectionString))
        {
            await UserSeed.AddAsync(context, user);
        }

        using var baseFactory = new PostgresApiFactory(postgres.ConnectionString);
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Jwt:SigningKey", RecognizableKey);
            builder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.SetMinimumLevel(LogLevel.Trace);
                logging.AddProvider(logs);
            });
        });
        var client = factory.CreateDefaultClient();
        client.DefaultRequestHeaders.Authorization = null;

        var responses = new List<HttpResponseMessage>
        {
            await LoginEndpointTests.PostLoginAsync(client, user.Email, UserSeed.Password),
            await client.GetAsync("/api/auth/me", CancellationToken.None),
            await client.GetAsync("/openapi/v1.json", CancellationToken.None),
        };

        foreach (var response in responses)
        {
            var headers = string.Join('\n', response.Headers.Concat(response.Content.Headers).SelectMany(header => header.Value));
            Assert.DoesNotContain(RecognizableKey, headers, StringComparison.Ordinal);
            Assert.DoesNotContain(RecognizableKey, await response.Content.ReadAsStringAsync(CancellationToken.None), StringComparison.Ordinal);
        }

        Assert.NotEmpty(logs.Messages);
        Assert.DoesNotContain(logs.Messages, message => message.Contains(RecognizableKey, StringComparison.Ordinal));
    }
}
