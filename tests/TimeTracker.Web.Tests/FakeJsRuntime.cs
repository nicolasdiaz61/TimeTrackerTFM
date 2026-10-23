using Microsoft.JSInterop;

namespace TimeTracker.Web.Tests;

/// <summary>Sustituto de js/session-storage.js: registra las llamadas y guarda en diccionarios lo que la Web escribe.</summary>
internal sealed class FakeJsRuntime : IJSRuntime
{
    private readonly Dictionary<string, string> shared = [];
    private readonly Dictionary<string, string> legacy = [];

    public List<string> Calls { get; } = [];

    public object?[]? SubscribeArguments { get; private set; }

    public void SeedLegacy(string key, string value) => legacy[key] = value;

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        Calls.Add(identifier);

        var key = args is { Length: > 0 } ? args[0] as string : null;
        switch (identifier)
        {
            case "timeTrackerSession.get":
                return ValueTask.FromResult((TValue)(object?)shared.GetValueOrDefault(key!)!);
            case "timeTrackerSession.set":
                shared[key!] = (string)args![1]!;
                break;
            case "timeTrackerSession.remove":
                shared.Remove(key!);
                break;
            case "timeTrackerSession.takeLegacy":
                var value = legacy.GetValueOrDefault(key!);
                legacy.Remove(key!);
                return ValueTask.FromResult((TValue)(object?)value!);
            case "timeTrackerSession.subscribe":
                SubscribeArguments = args;
                break;
            case "timeTrackerSession.unsubscribe":
                break;
            default:
                throw new NotSupportedException(identifier);
        }

        return ValueTask.FromResult(default(TValue)!);
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        => InvokeAsync<TValue>(identifier, args);
}
