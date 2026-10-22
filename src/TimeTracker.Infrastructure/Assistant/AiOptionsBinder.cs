using System.Globalization;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace TimeTracker.Infrastructure.Assistant;

/// <summary>
/// Enlaza la sección <c>Ai</c>. El enlazador de configuración, ante un entero no numérico, lanza una excepción cuyo mensaje repite el
/// valor; los topes por compañía se comprueban antes para fallar nombrando solo la clave (assistant-configuration, «Valor no numérico»).
/// </summary>
internal static class AiOptionsBinder
{
    private static readonly string[] CompanyLimitKeys =
    [
        nameof(AiOptions.RequestsPerMinutePerCompany),
        nameof(AiOptions.DailyRequestsPerCompany),
        nameof(AiOptions.MaxLoadedCompanies),
        nameof(AiOptions.RuntimeIdleMinutes),
    ];

    public static void Bind(AiOptions options, IConfiguration configuration)
    {
        var section = configuration.GetSection(AiOptions.SectionName);
        var notNumeric = CompanyLimitKeys
            .Where(key => section[key] is { Length: > 0 } value && !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            .Select(key => $"{AiOptions.SectionName}:{key}: debe ser un número entero.")
            .ToList();
        if (notNumeric.Count > 0)
        {
            throw new OptionsValidationException(Options.DefaultName, typeof(AiOptions), notNumeric);
        }

        section.Bind(options);
    }
}
