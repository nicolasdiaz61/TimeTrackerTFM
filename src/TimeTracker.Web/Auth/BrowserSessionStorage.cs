using Microsoft.JSInterop;

namespace TimeTracker.Web.Auth;

/// <summary>
/// Implementación sobre <c>wwwroot/js/session-storage.js</c> (sin <c>eval</c>, válida con la CSP de la Web). El JavaScript es un
/// envoltorio mínimo del almacenamiento local y del evento «storage»; toda la lógica vive en C#.
/// </summary>
public sealed class BrowserSessionStorage(IJSRuntime js) : ISessionStorage, IAsyncDisposable
{
    private DotNetObjectReference<BrowserSessionStorage>? reference;
    private Func<string?, string?, Task>? onChanged;

    public ValueTask<string?> GetAsync(string key) => js.InvokeAsync<string?>("timeTrackerSession.get", key);

    public ValueTask SetAsync(string key, string value) => js.InvokeVoidAsync("timeTrackerSession.set", key, value);

    public ValueTask RemoveAsync(string key) => js.InvokeVoidAsync("timeTrackerSession.remove", key);

    public ValueTask<string?> TakeLegacyAsync(string key) => js.InvokeAsync<string?>("timeTrackerSession.takeLegacy", key);

    public async ValueTask ListenAsync(Func<string?, string?, Task> callback)
    {
        await StopListeningAsync();
        onChanged = callback;
        reference = DotNetObjectReference.Create(this);
        await js.InvokeVoidAsync("timeTrackerSession.subscribe", reference);
    }

    public async ValueTask StopListeningAsync()
    {
        if (reference is null)
        {
            return;
        }

        reference.Dispose();
        reference = null;
        onChanged = null;
        await js.InvokeVoidAsync("timeTrackerSession.unsubscribe");
    }

    /// <summary>Lo invoca el navegador cuando otra pestaña cambia el almacenamiento local.</summary>
    [JSInvokable]
    public Task OnStorageChanged(string? key, string? newValue) => onChanged?.Invoke(key, newValue) ?? Task.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        try
        {
            await StopListeningAsync();
        }
        catch (JSException)
        {
            // El navegador ya descartó el escucha (por ejemplo, la página se está cerrando).
        }
        catch (JSDisconnectedException)
        {
            // Sin circuito de JavaScript: no queda nada que desuscribir.
        }
    }
}
