using Microsoft.Extensions.AI;

namespace TimeTracker.Infrastructure.Assistant;

/// <summary>
/// Configuración efectiva del asistente en un instante (DD-10): el pipeline ya montado, los valores con los que se creó y la
/// versión de la fila de la que salen (<c>"0"</c> = entorno). No cambia nunca: un cambio publica otra instantánea.
/// </summary>
internal sealed record AssistantSnapshot(IChatClient? Pipeline, AiOptions Options, string Version)
{
    /// <summary>Versión de la instantánea construida solo con el entorno.</summary>
    public const string EnvironmentVersion = "0";

    /// <summary>
    /// Copia defensiva: el motor solo necesita modelo, plazo, tokens y esfuerzo, así que ni la clave del proveedor ni las
    /// claves maestras se conservan en la instantánea.
    /// </summary>
    public static AssistantSnapshot Create(IChatClient? inner, AiOptions options, string version)
    {
        var copy = options.Copy();
        copy.ApiKey = null;
        copy.MasterKeys = [];
        return new AssistantSnapshot(inner is null ? null : AssistantChatPipeline.Build(inner, options), copy, version);
    }
}
