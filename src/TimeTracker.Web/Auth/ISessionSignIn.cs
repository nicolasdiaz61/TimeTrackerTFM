namespace TimeTracker.Web.Auth;

/// <summary>Guarda (o sustituye) el token de la sesión; lo implementa el proveedor de autenticación y lo falsean las pruebas.</summary>
public interface ISessionSignIn
{
    Task SignInAsync(string token);
}
