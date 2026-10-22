namespace TimeTracker.Api.Employees;

/// <summary>Contraseña temporal regenerada: se devuelve una sola vez, en la respuesta de la regeneración.</summary>
public sealed record TemporaryPasswordResponse(string TemporaryPassword)
{
    public override string ToString() => nameof(TemporaryPasswordResponse);
}
