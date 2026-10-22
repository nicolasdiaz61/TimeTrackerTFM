namespace TimeTracker.Application.Companies;

/// <summary>
/// Decide si un código de invitación es uno de los configurados. Devuelve solo aceptado o rechazado: ni el valor ni la posición del
/// código aceptado salen de aquí, de modo que nada que se pueda registrar los delata (DD-19, Q-12).
/// </summary>
public interface IInviteCodeValidator
{
    bool IsValid(string? candidate);
}
