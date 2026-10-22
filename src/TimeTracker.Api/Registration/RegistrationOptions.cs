namespace TimeTracker.Api.Registration;

/// <summary>
/// Opciones del autorregistro de compañías (DD-19, DD-23). Cerrado por defecto: sin <c>Registration__Enabled=true</c> la ruta no
/// existe. Ninguna de las claves secretas figura en <c>appsettings*.json</c>: los códigos son un secreto del entorno de despliegue.
/// </summary>
public sealed class RegistrationOptions
{
    public const string SectionName = "Registration";

    public const int DefaultGlobalHourlyLimit = 30;

    public const int MaxGlobalHourlyLimit = 1000;

    /// <summary>Máximo de particiones vivas del límite por IP; pasado este número, las claves nuevas comparten una partición de desbordamiento.</summary>
    public const int MaxTrackedPartitions = 10_000;

    /// <summary>Ventana común del límite por IP (5 por hora) y del tope global.</summary>
    public static readonly TimeSpan LimitWindow = TimeSpan.FromHours(1);

    // Un valor no booleano falla al enlazar y el mensaje del binder nombra la clave.
    public bool Enabled { get; set; }

    // Texto para que el validador controle el mensaje y ningún código se repita en un error: lista separada por comas.
    public string? InviteCodes { get; set; }

    /// <summary>Altas por hora y en total (todas las IP juntas) que acepta la instancia. Por defecto 30; rango válido: 1 a <see cref="MaxGlobalHourlyLimit"/>.</summary>
    public int GlobalHourlyLimit { get; set; } = DefaultGlobalHourlyLimit;

    /// <summary>
    /// Los códigos de la lista, recortados. Los elementos en blanco se conservan como cadenas vacías para que el validador los
    /// rechace; sin lista devuelve una lista vacía.
    /// </summary>
    public IReadOnlyList<string> ParseInviteCodes()
        => string.IsNullOrWhiteSpace(InviteCodes) ? [] : [.. InviteCodes.Split(',').Select(code => code.Trim())];
}
