using Microsoft.JSInterop;

namespace TimeTracker.Web.Tests;

/// <summary>Sustituto de sessionStorage: guarda en un diccionario lo que la Web escribe con getItem/setItem/removeItem.</summary>
internal sealed class FakeJsRuntime : IJSRuntime
{
    private readonly Dictionary<string, string> storage = [];

    public void Seed(string key, string value) => storage[key] = value;

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        var key = (string)args![0]!;
        switch (identifier)
        {
            case "sessionStorage.getItem":
                return ValueTask.FromResult((TValue)(object?)storage.GetValueOrDefault(key)!);
            case "sessionStorage.setItem":
                storage[key] = (string)args[1]!;
                break;
            case "sessionStorage.removeItem":
                storage.Remove(key);
                break;
            default:
                throw new NotSupportedException(identifier);
        }

        return ValueTask.FromResult(default(TValue)!);
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        => InvokeAsync<TValue>(identifier, args);
}
