using System.Text;

using Microsoft.Extensions.Options;

namespace TimeTracker.Api.Authentication;

public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    // HMAC-SHA256 exige una clave de al menos el tamaño del resumen (RFC 7518, 3.2).
    public const int MinimumSigningKeyBytes = 32;

    private const string Key = "Jwt:SigningKey";

    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            return ValidateOptionsResult.Fail($"{Key}: debe definirse.");
        }

        // Se miden bytes y no caracteres: 16 'ñ' ocupan 32 bytes. El mensaje nunca incluye el valor.
        return Encoding.UTF8.GetByteCount(options.SigningKey) < MinimumSigningKeyBytes
            ? ValidateOptionsResult.Fail($"{Key}: debe tener al menos {MinimumSigningKeyBytes} bytes.")
            : ValidateOptionsResult.Success;
    }
}
