using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Npgsql;

using TimeTracker.Application.Assistant.Configuration;
using TimeTracker.Infrastructure.Assistant;
using TimeTracker.Infrastructure.Persistence;
using TimeTracker.Infrastructure.Tests.Fakes;

namespace TimeTracker.Api.IntegrationTests.Assistant;

/// <summary>
/// API con PostgreSQL real y el sondeo de la configuración guardada activo (cada segundo). El proveedor se sustituye por una
/// fábrica falsa que responde «respuesta de {modelo}», así que el modelo usado se lee de la respuesta del chat. El reloj de la
/// aplicación es ficticio (FakeTimeProvider): el sondeo avanza con PollOnceAsync, no con el tiempo real.
/// </summary>
internal sealed class ReloadApiFactory(string connectionString, bool applyMigrations = false) : ApiFactory
{
    public const string EnvironmentModel = "modelo-env";

    public RecordingChatClientFactory ChatFactory { get; } = new();

    public StoreObserver Observer { get; } = new(connectionString);

    protected override bool KeepsAssistantSettingsPoller => true;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("ConnectionStrings:Default", connectionString);
        builder.UseSetting("Database:ApplyMigrationsOnStartup", applyMigrations ? "true" : "false");
        builder.UseSetting("Ai:Provider", "Ollama");
        builder.UseSetting("Ai:Model", EnvironmentModel);
        builder.UseSetting("Ai:Endpoint", "http://localhost:11434/");
        builder.UseSetting("Ai:SettingsPollSeconds", "1");
        builder.ConfigureTestServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<IChatClientFactory>(ChatFactory));
            services.Replace(ServiceDescriptor.Scoped<IAssistantSettingsStore>(
                provider => new ObservingSettingsStore(ActivatorUtilities.CreateInstance<AssistantSettingsStore>(provider), Observer)));
        });
    }

    /// <summary>Avanza el reloj un intervalo de sondeo y deja que el sondeo termine su vuelta.</summary>
    public async Task PollOnceAsync()
    {
        var reads = Observer.Reads;
        Clock.Advance(TimeSpan.FromSeconds(1));
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (Observer.Reads == reads)
        {
            Assert.True(DateTime.UtcNow < deadline, "El sondeo no leyó la versión a tiempo.");
            await Task.Delay(10);
        }

        // La carga que sigue a la lectura de la versión es asíncrona: se deja un margen corto para que publique la instantánea.
        await Task.Delay(100);
    }

    public static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}

/// <summary>Registra cuántas veces se leyó la configuración y si la tabla existía en la primera lectura (orden de arranque, V-6).</summary>
internal sealed class StoreObserver(string connectionString)
{
    private int _reads;

    public int Reads => Volatile.Read(ref _reads);

    public bool? TableExistedAtFirstRead { get; private set; }

    public async Task OnReadAsync()
    {
        if (Interlocked.Increment(ref _reads) != 1)
        {
            return;
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT to_regclass('assistant_settings') IS NOT NULL", connection);
        TableExistedAtFirstRead = (bool?)await command.ExecuteScalarAsync();
    }
}

internal sealed class ObservingSettingsStore(IAssistantSettingsStore inner, StoreObserver observer) : IAssistantSettingsStore
{
    public async Task<AssistantSettings?> GetAsync(CancellationToken cancellationToken)
    {
        await observer.OnReadAsync();
        return await inner.GetAsync(cancellationToken);
    }

    public async Task<string> GetVersionAsync(CancellationToken cancellationToken)
    {
        await observer.OnReadAsync();
        return await inner.GetVersionAsync(cancellationToken);
    }

    public Task<SaveOutcome> SaveAsync(AssistantSettings next, string expectedToken, AuditEntry audit, CancellationToken cancellationToken)
        => inner.SaveAsync(next, expectedToken, audit, cancellationToken);

    public Task<DeleteOutcome> DeleteAsync(string expectedToken, AuditEntry audit, CancellationToken cancellationToken)
        => inner.DeleteAsync(expectedToken, audit, cancellationToken);
}
