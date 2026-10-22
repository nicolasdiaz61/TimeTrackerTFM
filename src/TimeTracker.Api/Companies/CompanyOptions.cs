using System.Globalization;

using Microsoft.Extensions.Options;

using TimeTracker.Application.Companies;

namespace TimeTracker.Api.Companies;

/// <summary>
/// Topes por compañía que fija el entorno (DD-16, DD-23): <c>Company__MaxEmployees</c>, 50 por defecto, de 1 a 1000. Ningún Manager
/// los edita y la clave no figura en <c>appsettings*.json</c>.
/// </summary>
public sealed class CompanyOptions
{
    public const string SectionName = "Company";

    public const int DefaultMaxEmployees = 50;

    public const int MinMaxEmployees = 1;

    public const int MaxMaxEmployees = 1000;

    public int MaxEmployees { get; set; } = DefaultMaxEmployees;

    /// <summary>
    /// Enlaza la sección. El enlazador de configuración, ante un entero no numérico, lanza una excepción cuyo mensaje repite el valor; se
    /// comprueba antes para fallar nombrando solo la clave (startup-configuration, «Valor no numérico»).
    /// </summary>
    internal static void Bind(CompanyOptions options, IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        if (section[nameof(MaxEmployees)] is { Length: > 0 } text && !int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            throw new OptionsValidationException(
                Options.DefaultName, typeof(CompanyOptions), [$"{SectionName}:{nameof(MaxEmployees)}: debe ser un número entero."]);
        }

        section.Bind(options);
    }
}

/// <summary>Valida el rango del tope al arrancar; el mensaje nombra la clave y nunca imprime el valor.</summary>
public sealed class CompanyOptionsValidator : IValidateOptions<CompanyOptions>
{
    public ValidateOptionsResult Validate(string? name, CompanyOptions options)
        => options.MaxEmployees is >= CompanyOptions.MinMaxEmployees and <= CompanyOptions.MaxMaxEmployees
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"{CompanyOptions.SectionName}:{nameof(CompanyOptions.MaxEmployees)}: debe estar entre {CompanyOptions.MinMaxEmployees} y {CompanyOptions.MaxMaxEmployees}.");
}

/// <summary>Adaptador del puerto <see cref="ICompanyLimits"/> sobre las opciones del entorno.</summary>
public sealed class ConfiguredCompanyLimits(IOptions<CompanyOptions> options) : ICompanyLimits
{
    public int MaxEmployees => options.Value.MaxEmployees;
}
