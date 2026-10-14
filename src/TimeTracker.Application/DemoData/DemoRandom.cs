namespace TimeTracker.Application.DemoData;

/// <summary>
/// Generador SplitMix64 propio: <c>System.Random</c> con semilla no garantiza la misma secuencia entre versiones de .NET.
/// El estado inicial mezcla la semilla del perfil con el número de día, de modo que cada día es independiente de la ventana.
/// </summary>
internal struct DemoRandom
{
    private const ulong Gamma = 0x9E3779B97F4A7C15UL;
    private ulong _state;

    public DemoRandom(ulong seed, int dayNumber) => _state = seed ^ ((ulong)dayNumber * Gamma);

    /// <summary>Entero en <c>[minInclusive, maxExclusive)</c>.</summary>
    public int Next(int minInclusive, int maxExclusive)
        => minInclusive + (int)(NextValue() % (ulong)(maxExclusive - minInclusive));

    private ulong NextValue()
    {
        _state += Gamma;
        var z = _state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
