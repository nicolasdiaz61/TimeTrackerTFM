namespace TimeTracker.Api.Authentication;

/// <summary>
/// Clave de firma versionada en <c>appsettings.Development.json</c> para que la API arranque en local sin variables de entorno (ADR 0032).
/// Es pública por estar en el repositorio: <see cref="JwtOptionsValidator"/> impide usarla fuera de Development, y una prueba
/// comprueba que el JSON y esta constante coinciden.
/// </summary>
public static class DevelopmentSigningKey
{
    public const string Value = "clave-de-desarrollo-local-no-usar-en-produccion-7c1e9f3a5b2d";
}
