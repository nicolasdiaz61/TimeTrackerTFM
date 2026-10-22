using TimeTracker.Application.Companies;

namespace TimeTracker.Api.IntegrationTests.Support;

/// <summary>
/// Cuenta cuántas veces se evalúa un código de invitación. Con un validador interno delega en él (para ver que el sexto intento no
/// lo evalúa); sin él rechaza siempre (para ver que una ruta cerrada nunca lo evalúa).
/// </summary>
internal sealed class CountingInviteCodeValidator(IInviteCodeValidator? inner = null) : IInviteCodeValidator
{
    private int _calls;

    public int Calls => Volatile.Read(ref _calls);

    public bool IsValid(string? candidate)
    {
        Interlocked.Increment(ref _calls);
        return inner?.IsValid(candidate) ?? false;
    }
}
