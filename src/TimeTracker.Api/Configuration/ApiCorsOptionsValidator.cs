using Microsoft.Extensions.Options;

namespace TimeTracker.Api.Configuration;

public sealed class ApiCorsOptionsValidator : IValidateOptions<ApiCorsOptions>
{
    private const string Key = "Cors:AllowedOrigins";

    public ValidateOptionsResult Validate(string? name, ApiCorsOptions options)
    {
        if (options.AllowedOrigins is null or { Count: 0 })
        {
            return ValidateOptionsResult.Fail($"{Key}: debe definirse al menos un origen permitido.");
        }

        var failures = new List<string>();
        for (var index = 0; index < options.AllowedOrigins.Count; index++)
        {
            var reason = FindProblem(options.AllowedOrigins[index]);
            if (reason is not null)
            {
                failures.Add($"{Key}:{index}: {reason}");
            }
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    // Devuelve solo la razón: nunca incluye el valor recibido, para no filtrar configuración en los registros.
    private static string? FindProblem(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin) || origin == "*")
        {
            return "no puede estar en blanco ni ser un comodín.";
        }

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return "debe ser una URL absoluta.";
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return "el esquema debe ser http o https.";
        }

        if (uri.AbsolutePath != "/" || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return "no debe incluir ruta, consulta, fragmento ni datos de usuario.";
        }

        if (origin.EndsWith('/'))
        {
            return "no debe terminar en barra.";
        }

        return null;
    }
}
