using Microsoft.Extensions.Options;

using TimeTracker.Api.Configuration;
using TimeTracker.Api.Seeding;
using TimeTracker.Application.Companies;

namespace TimeTracker.Api.Registration;

/// <summary>
/// Valida al arrancar el registro y su entorno (DD-19, DD-22, DD-23). Los mensajes nombran las claves y la razón y nunca imprimen un
/// código ni otro valor secreto. Con el registro cerrado no se exige ni se valida nada. Se evalúa al leer las opciones, antes de que
/// ningún servicio alojado aplique migraciones o siembre, de modo que un fallo no deja ningún cambio en la base de datos.
/// </summary>
public sealed class RegistrationOptionsValidator(IConfiguration configuration, IOptions<SeedOptions> seed, IHostEnvironment environment)
    : IValidateOptions<RegistrationOptions>
{
    public const int MinimumCodeLength = 16;

    public ValidateOptionsResult Validate(string? name, RegistrationOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();
        AddInviteCodeFailures(options, failures);
        AddDemoSeedFailure(failures);
        AddForwardedHeadersFailure(failures);

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void AddInviteCodeFailures(RegistrationOptions options, List<string> failures)
    {
        var codes = options.ParseInviteCodes();
        if (codes.Count == 0)
        {
            failures.Add("Registration:InviteCodes: se requiere al menos un código de invitación cuando Registration:Enabled es true.");
        }
        else if (codes.Any(code => code.Length is < MinimumCodeLength or > InviteCodeValidator.MaximumLength))
        {
            failures.Add($"Registration:InviteCodes: cada código debe tener entre {MinimumCodeLength} y {InviteCodeValidator.MaximumLength} caracteres y no puede estar en blanco.");
        }
    }

    // La siembra crea usuarios con una contraseña pública (README): jamás deben coexistir con el registro abierto (DD-22).
    private void AddDemoSeedFailure(List<string> failures)
    {
        if (seed.Value.DemoUsers)
        {
            failures.Add("Registration:Enabled y Seed:DemoUsers no pueden ser true a la vez: la siembra de demostración crea usuarios con una contraseña pública y no debe coexistir con el registro abierto. Desactive Seed:DemoUsers (y Seed:DemoData) o cierre el registro.");
        }
    }

    // Sin ForwardedHeaders el límite por IP comparte una sola partición para todos los clientes o puede falsearse (DD-19, ADR 0013).
    // Un número de saltos menor que 1 con ForwardedHeaders activo ya detiene el arranque al registrar el tratamiento de las cabeceras
    // (ForwardedHeadersExtensions), nombrando ForwardedHeaders:Hops, por lo que aquí basta con exigir que esté activo.
    private void AddForwardedHeadersFailure(List<string> failures)
    {
        if (!environment.IsProduction())
        {
            return;
        }

        var forwarded = configuration.GetSection(ForwardedHeadersSettings.SectionName).Get<ForwardedHeadersSettings>() ?? new ForwardedHeadersSettings();
        if (!forwarded.Enabled)
        {
            failures.Add("ForwardedHeaders:Enabled: debe ser true cuando Registration:Enabled es true en Production; sin él, el límite por IP del registro comparte una sola partición para todos los clientes o puede falsearse con una cabecera X-Forwarded-For del cliente.");
        }
    }
}
