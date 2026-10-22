namespace TimeTracker.Api.Registration;

/// <summary>Reserva de un cupo global; identifica la ventana para que una liberación tardía no reste de la ventana siguiente.</summary>
public readonly record struct RegistrationReservation(long Window);

/// <summary>
/// Tope global de altas del registro por ventana fija (todas las IP juntas): limita el daño con un código de invitación filtrado.
/// Se reserva un cupo antes de ejecutar el registro y se libera si no llega a crearse nada, de modo que solo cuentan las altas y
/// que los intentos con código incorrecto no agoten el tope. Estado en memoria, como los demás límites de tasa (instancia única).
/// </summary>
public sealed class RegistrationGlobalLimiter(TimeProvider clock, int limit, TimeSpan window)
{
    private readonly Lock _gate = new();
    private DateTimeOffset _windowStart = DateTimeOffset.MinValue;
    private int _count;

    /// <summary>Devuelve la reserva, o <see langword="null"/> si el tope se alcanzó (y en <paramref name="retryAfter"/> lo que falta de ventana).</summary>
    public RegistrationReservation? Reserve(out TimeSpan retryAfter)
    {
        var now = clock.GetUtcNow();
        lock (_gate)
        {
            if (now - _windowStart >= window)
            {
                _windowStart = now;
                _count = 0;
            }

            if (_count >= limit)
            {
                retryAfter = _windowStart + window - now;
                return null;
            }

            _count++;
            retryAfter = TimeSpan.Zero;
            return new RegistrationReservation(_windowStart.UtcTicks);
        }
    }

    public void Release(RegistrationReservation reservation)
    {
        lock (_gate)
        {
            if (reservation.Window == _windowStart.UtcTicks && _count > 0)
            {
                _count--;
            }
        }
    }
}
