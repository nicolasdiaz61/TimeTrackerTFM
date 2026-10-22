namespace TimeTracker.Application.Authentication;

/// <summary>Puerto que genera la contraseña temporal de una persona nueva o regenerada (DD-21).</summary>
public interface ITemporaryPasswordGenerator
{
    /// <summary>Devuelve una contraseña nueva de al menos 16 caracteres que cumple la política de contraseñas.</summary>
    string Generate();
}
