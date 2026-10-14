namespace TimeTracker.Web.Auth;

/// <summary>
/// Aviso en memoria de que la sesión terminó por caducidad o por un 401. Evita viajar por la URL: /login lo consume una sola vez,
/// así que un inicio de sesión normal o un cierre voluntario nunca lo muestran.
/// </summary>
public sealed class SessionNotice
{
    private bool expired;

    public void MarkExpired() => expired = true;

    public void Clear() => expired = false;

    /// <summary>Devuelve si había un aviso pendiente y lo descarta.</summary>
    public bool Consume()
    {
        var pending = expired;
        expired = false;
        return pending;
    }
}
