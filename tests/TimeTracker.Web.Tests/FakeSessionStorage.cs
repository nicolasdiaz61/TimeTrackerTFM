using TimeTracker.Web.Auth;

namespace TimeTracker.Web.Tests;

/// <summary>Almacenamiento compartido falso: un diccionario para el almacenamiento local y otro para el heredado por pestaña.</summary>
internal sealed class FakeSessionStorage : ISessionStorage
{
    private readonly Dictionary<string, string> shared = [];
    private readonly Dictionary<string, string> legacy = [];
    private Func<string?, string?, Task>? listener;

    /// <summary>Simula un navegador que bloquea el almacenamiento: toda operación lanza.</summary>
    public bool Blocked { get; set; }

    public bool Listening => listener is not null;

    public string? Stored(string key) => shared.GetValueOrDefault(key);

    public string? StoredLegacy(string key) => legacy.GetValueOrDefault(key);

    public void Seed(string key, string value) => shared[key] = value;

    public void SeedLegacy(string key, string value) => legacy[key] = value;

    /// <summary>Simula un cambio hecho por otra pestaña (evento «storage»).</summary>
    public Task RaiseAsync(string? key, string? newValue)
    {
        if (key is null)
        {
            shared.Clear();
        }
        else if (newValue is null)
        {
            shared.Remove(key);
        }
        else
        {
            shared[key] = newValue;
        }

        return listener?.Invoke(key, newValue) ?? Task.CompletedTask;
    }

    public ValueTask<string?> GetAsync(string key)
    {
        ThrowIfBlocked();
        return ValueTask.FromResult(shared.GetValueOrDefault(key));
    }

    public ValueTask SetAsync(string key, string value)
    {
        ThrowIfBlocked();
        shared[key] = value;
        return ValueTask.CompletedTask;
    }

    public ValueTask RemoveAsync(string key)
    {
        ThrowIfBlocked();
        shared.Remove(key);
        return ValueTask.CompletedTask;
    }

    public ValueTask<string?> TakeLegacyAsync(string key)
    {
        ThrowIfBlocked();
        var value = legacy.GetValueOrDefault(key);
        legacy.Remove(key);
        return ValueTask.FromResult(value);
    }

    public ValueTask ListenAsync(Func<string?, string?, Task> onChanged)
    {
        ThrowIfBlocked();
        listener = onChanged;
        return ValueTask.CompletedTask;
    }

    public ValueTask StopListeningAsync()
    {
        listener = null;
        return ValueTask.CompletedTask;
    }

    private void ThrowIfBlocked()
    {
        if (Blocked)
        {
            throw new InvalidOperationException("Almacenamiento bloqueado.");
        }
    }
}
