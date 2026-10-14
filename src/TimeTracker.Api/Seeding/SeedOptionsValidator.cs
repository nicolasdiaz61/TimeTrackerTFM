using Microsoft.Extensions.Options;

namespace TimeTracker.Api.Seeding;

public sealed class SeedOptionsValidator : IValidateOptions<SeedOptions>
{
    public const int MinimumPasswordLength = 12;

    public ValidateOptionsResult Validate(string? name, SeedOptions options)
    {
        // Los fichajes demo pertenecen a los usuarios demo: sin ellos no hay a quién asignarlos.
        if (options.DemoData && !options.DemoUsers)
        {
            return ValidateOptionsResult.Fail("Seed:DemoData: requiere Seed:DemoUsers=true (los fichajes de demostración se asignan a los usuarios de demostración).");
        }

        // Con la siembra desactivada la contraseña no se exige ni se valida (SC-12).
        if (!options.DemoUsers)
        {
            return ValidateOptionsResult.Success;
        }

        // El mensaje nunca incluye el valor de la contraseña.
        return (options.DemoPassword?.Length ?? 0) < MinimumPasswordLength
            ? ValidateOptionsResult.Fail($"Seed:DemoPassword: debe tener al menos {MinimumPasswordLength} caracteres cuando Seed:DemoUsers es true.")
            : ValidateOptionsResult.Success;
    }
}
