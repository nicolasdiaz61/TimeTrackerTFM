namespace TimeTracker.Web.Team;

/// <summary>
/// Contraseña temporal mostrada UNA SOLA VEZ. Solo vive en la memoria del componente: no se guarda en el almacenamiento del
/// navegador, ni en la URL, y desaparece al cerrar el aviso, al empezar otra operación o al navegar a otra ruta.
/// </summary>
public sealed class TemporaryPasswordReveal
{
    public const string Notice =
        "Entregue esta contraseña a la persona ahora: no se volverá a mostrar. Deberá cambiarla en su primer inicio de sesión.";

    public string? Value { get; private set; }

    public string? PersonName { get; private set; }

    public bool IsVisible => Value is not null;

    public void Show(string personName, string temporaryPassword)
    {
        ArgumentNullException.ThrowIfNull(personName);
        ArgumentNullException.ThrowIfNull(temporaryPassword);

        PersonName = personName;
        Value = temporaryPassword;
    }

    public void Dismiss()
    {
        Value = null;
        PersonName = null;
    }
}
