using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.AI;

namespace TimeTracker.Infrastructure.Assistant;

/// <summary>Serialización de los resultados de las herramientas (DD-7): camelCase del SDK y enumeraciones como texto.</summary>
internal static class AssistantJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(AIJsonUtilities.DefaultOptions);
        options.Converters.Add(new JsonStringEnumConverter());
        options.WriteIndented = false;
        options.MakeReadOnly();
        return options;
    }
}
