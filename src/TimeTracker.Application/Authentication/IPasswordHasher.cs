namespace TimeTracker.Application.Authentication;

public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>
    /// Verifica la contraseña. Con <paramref name="passwordHash"/> nulo (usuario inexistente) hace un trabajo
    /// equivalente contra un hash ficticio y devuelve <c>false</c>, para no revelar si el correo existe por el tiempo de respuesta.
    /// </summary>
    bool Verify(string? passwordHash, string password);
}
