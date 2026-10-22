namespace TimeTracker.Api.Security;

/// <summary>
/// Marca un endpoint cuyo contexto de compañía no debe fijarse desde el token, aunque la petición lleve uno vigente (DD-7). Solo lo
/// lleva el registro de compañías: es anónimo, ignora el token y crea la compañía nueva en su propio ámbito. Una prueba de
/// arquitectura exige que ningún otro endpoint lo use.
/// </summary>
public sealed class SkipCompanyContextMetadata;

public static class SkipCompanyContextExtensions
{
    public static TBuilder SkipCompanyContext<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
        => builder.WithMetadata(new SkipCompanyContextMetadata());
}
