using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Npgsql;

using TimeTracker.Api.IntegrationTests.Support;

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
internal sealed class ReloadApiFactory(string connectionString, bool applyMigrations = false, string? masterKey = null) : ApiFactory
{
    public const string EnvironmentModel = "modelo-env";

    public RecordingChatClientFactory ChatFactory { get; } = new();

    public StoreObserver Observer { get; } = new(connectionString);

    /// <summary>Todo lo que registra esta instancia, para comprobar avisos y que ningún secreto llega al registro.</summary>
    public CapturingLoggerProvider Logs { get; } = new();

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
        builder.UseSetting("Ai:AllowedProviders", "Anthropic,OpenAI,Ollama");
        if (masterKey is not null)
        {
            builder.UseSetting("Ai:MasterKeys:1", masterKey);
            builder.UseSetting("Ai:ActiveMasterKeyVersion", "1");
        }

        builder.ConfigureLogging(logging => logging.AddProvider(Logs));
        builder.ConfigureTestServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<IChatClientFactory>(ChatFactory));
            services.Replace(ServiceDescriptor.Scoped<IAssistantSettingsStore>(
                provider => new ObservingSettingsStore(ActivatorUtilities.CreateInstance<AssistantSettingsStore>(provider), Observer)));
        });
    }

    /// <summary>Avanza el reloj un intervalo de sondeo y espera la señal explícita de «vuelta terminada» (la instantánea ya está publicada).</summary>
    public async Task PollOnceAsync()
    {
        var poller = Services.GetServices<IHostedService>().OfType<AssistantSettingsPoller>().Single();
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnCompleted() => completed.TrySetResult();
        poller.PollCompleted += OnCompleted;
        try
        {
            Clock.Advance(TimeSpan.FromSeconds(1));
            await completed.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        finally
        {
            poller.PollCompleted -= OnCompleted;
        }
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
