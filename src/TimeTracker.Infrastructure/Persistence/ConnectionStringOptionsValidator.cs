using Microsoft.Extensions.Options;

using Npgsql;

namespace TimeTracker.Infrastructure.Persistence;

public sealed class ConnectionStringOptionsValidator : IValidateOptions<ConnectionStringOptions>
{
    private const string Key = "ConnectionStrings:Default";

    public ValidateOptionsResult Validate(string? name, ConnectionStringOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Default))
        {
            return ValidateOptionsResult.Fail($"{Key}: debe definirse con la cadena de conexión de PostgreSQL.");
        }

        try
        {
            _ = new NpgsqlConnectionStringBuilder(options.Default);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException)
        {
            // El mensaje del analizador puede repetir fragmentos de la cadena (contraseña incluida): no se propaga.
            return ValidateOptionsResult.Fail($"{Key}: el formato no es válido.");
        }

        return ValidateOptionsResult.Success;
    }
}
