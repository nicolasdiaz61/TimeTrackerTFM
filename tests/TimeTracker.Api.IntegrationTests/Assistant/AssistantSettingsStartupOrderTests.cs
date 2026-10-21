using System.Net;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Infrastructure.Assistant;

using static TimeTracker.Api.IntegrationTests.Assistant.AssistantClient;

namespace TimeTracker.Api.IntegrationTests.Assistant;

/// <summary>V-6: la primera carga de la configuración guardada ocurre tras las migraciones y antes del primer chat.</summary>
[Collection("Postgres")]
public sealed class AssistantSettingsStartupOrderTests(PostgresFixture postgres)
{
    private const string InsertRow = "INSERT INTO assistant_settings (id, company_id, provider, model, updated_by, updated_at) VALUES (1, '00000000-0000-7000-8000-000000000001', 'Ollama', 'modelo-db', gen_random_uuid(), now())";

    [Fact]
    public async Task TheFirstLoad_RunsAfterTheMigrationsAndBeforeTheHostFinishesStarting()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using var factory = new ReloadApiFactory(connectionString, applyMigrations: true);

        _ = factory.CreateClient();

        Assert.True(factory.Observer.Reads >= 1, "La primera carga no se hizo durante el arranque.");
        Assert.True(factory.Observer.TableExistedAtFirstRead, "La primera carga se adelantó a las migraciones.");
    }

    [Fact]
    public async Task TheFirstChat_UsesTheStoredRowThatWasLoadedDuringStartup()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        await using (var context = postgres.CreateContext(connectionString))
        {
            await context.Database.MigrateAsync();
        }

        await ReloadApiFactory.ExecuteAsync(connectionString, InsertRow);
        using var factory = new ReloadApiFactory(connectionString);

        var client = factory.CreateClient();
        Assert.Equal("modelo-db", factory.ChatFactory.Created.Last().Model);
        var response = await client.AskAsync("hola");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ReadAsync(response);
        Assert.Equal("respuesta de modelo-db", body.RootElement.GetProperty("reply").GetString());
    }

    [Fact]
    public async Task TheFirstChat_WithoutARow_UsesTheEnvironment()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using var factory = new ReloadApiFactory(connectionString, applyMigrations: true);

        var response = await factory.CreateClient().AskAsync("hola");

        using var body = await ReadAsync(response);
        Assert.Equal("respuesta de modelo-env", body.RootElement.GetProperty("reply").GetString());
    }

    [Fact]
    public async Task ThePollerIsRegistered_ByAFactoryThatExercisesIt_AndRemovedByTheDefaultOne()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using var withPoller = new ReloadApiFactory(connectionString, applyMigrations: true);
        using var withoutPoller = new ApiFactory();

        Assert.Single(withPoller.Services.GetServices<IHostedService>().OfType<AssistantSettingsPoller>());
        Assert.Empty(withoutPoller.Services.GetServices<IHostedService>().OfType<AssistantSettingsPoller>());
    }
}
