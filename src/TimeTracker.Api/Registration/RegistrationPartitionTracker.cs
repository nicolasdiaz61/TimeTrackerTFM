namespace TimeTracker.Api.Registration;

/// <summary>
/// Acota las particiones del límite por IP del registro. Cada clave dura una ventana; pasado el máximo de claves vivas, las nuevas
/// comparten la partición de <see cref="Overflow"/> (con el mismo límite), de modo que la memoria no crece sin tope aunque alguien
/// rote direcciones (sobre todo IPv6). Con el máximo agotado, quien llega nuevo comparte cupo con el resto: falla cerrado.
/// </summary>
public sealed class RegistrationPartitionTracker(TimeProvider clock, int maxPartitions, TimeSpan window)
{
    public const string Overflow = "desbordamiento";

    // Con el mapa lleno, la limpieza de claves caducadas se hace como mucho una vez por minuto para no recorrerlo en cada petición.
    private static readonly TimeSpan PruneInterval = TimeSpan.FromMinutes(1);

    private readonly Lock _gate = new();
    private readonly Dictionary<string, DateTimeOffset> _expiries = [];
    private DateTimeOffset _nextPrune = DateTimeOffset.MinValue;

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _expiries.Count;
            }
        }
    }

    public string Resolve(string key)
    {
        var now = clock.GetUtcNow();
        lock (_gate)
        {
            if (_expiries.TryGetValue(key, out var expiry) && expiry > now)
            {
                return key;
            }

            if (_expiries.Count >= maxPartitions && now >= _nextPrune)
            {
                Prune(now);
            }

            if (_expiries.Count >= maxPartitions && !_expiries.ContainsKey(key))
            {
                return Overflow;
            }

            _expiries[key] = now + window;
            return key;
        }
    }

    private void Prune(DateTimeOffset now)
    {
        foreach (var expired in _expiries.Where(entry => entry.Value <= now).Select(entry => entry.Key).ToList())
        {
            _expiries.Remove(expired);
        }

        _nextPrune = now + PruneInterval;
    }
}
