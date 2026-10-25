namespace TimeTracker.Api.Registration;

/// <summary>
/// Código de invitación versionado en <c>appsettings.Development.json</c> para poder registrar compañías en local sin configurar nada
/// (ADR 0033). Es público por estar en el repositorio: <see cref="RegistrationOptionsValidator"/> impide arrancar fuera de Development
/// con este código, y una prueba comprueba que el JSON y esta constante coinciden.
/// </summary>
public static class DevelopmentInviteCode
{
    public const string Value = "invitacion-desarrollo-local-4f8b2d6a1c9e";
}
