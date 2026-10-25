using Microsoft.Extensions.Configuration;

namespace TimeTracker.Web.Registration;

/// <summary>
/// Interruptor del enlace «Crear una compañía» del login (ADR 0033). Sale de la configuración de la Web (<c>Features:ShowRegisterLink</c>):
/// true en <c>appsettings.Development.json</c>, false en el base y, en un despliegue, de la variable <c>SHOW_REGISTER_LINK</c> del
/// servicio Web. Solo el booleano <c>true</c> lo activa; mostrar el enlace no abre el registro de la API.
/// </summary>
public static class RegisterLinkOption
{
    public const string Key = "Features:ShowRegisterLink";

    public static bool IsEnabled(IConfiguration configuration)
        => bool.TryParse(configuration[Key], out var enabled) && enabled;
}
