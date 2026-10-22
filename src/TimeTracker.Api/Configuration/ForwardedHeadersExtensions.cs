using Microsoft.AspNetCore.HttpOverrides;

namespace TimeTracker.Api.Configuration;

public static class ForwardedHeadersExtensions
{
    /// <summary>
    /// Registra el tratamiento de las cabeceras de proxy solo si <c>ForwardedHeaders:Enabled</c> es <c>true</c>. Las
    /// direcciones del borde del proveedor son públicas y cambian, así que no se fija una lista de proxies: se limita
    /// el número de saltos (<c>ForwardedHeaders:Hops</c>, 2 por defecto: «cliente, borde»). El cliente es la entrada
    /// situada tantos saltos como proxies de confianza hay a la derecha; lo que el cliente antepone queda más a la izquierda y
    /// nunca cuenta. Solo debe activarse cuando la API únicamente es alcanzable a través de ese proxy.
    /// </summary>
    public static IServiceCollection AddForwardedHeadersBehindProxy(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection(ForwardedHeadersSettings.SectionName).Get<ForwardedHeadersSettings>() ?? new ForwardedHeadersSettings();
        if (!settings.Enabled)
        {
            return services;
        }

        if (settings.Hops < 1)
        {
            // Sin saltos de confianza no se puede leer la IP real del cliente: el límite por IP (login y registro) compartiría una sola
            // partición o se podría falsear con una cabecera X-Forwarded-For del cliente. El mensaje no incluye el valor recibido.
            throw new InvalidOperationException(
                $"{ForwardedHeadersSettings.SectionName}:{nameof(ForwardedHeadersSettings.Hops)}: debe ser al menos 1; sin saltos de proxy de confianza el límite por IP comparte una sola partición para todos los clientes o puede falsearse con una cabecera X-Forwarded-For del cliente.");
        }

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = settings.Hops;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });
        services.AddSingleton<ForwardedHeadersMarker>();

        return services;
    }

    /// <summary>Debe ir el primero del pipeline: el resto (limitador, CORS, cabeceras) ve ya la IP y el esquema reales.</summary>
    public static IApplicationBuilder UseForwardedHeadersBehindProxy(this IApplicationBuilder app)
        => app.ApplicationServices.GetService<ForwardedHeadersMarker>() is null ? app : app.UseForwardedHeaders();

    private sealed class ForwardedHeadersMarker;
}
