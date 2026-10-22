namespace TimeTracker.Api.Registration;

/// <summary>
/// Opciones del autorregistro de compañías (DD-19, DD-23). Cerrado por defecto: sin <c>Registration__Enabled=true</c> la ruta no
/// existe. Ninguna de las dos claves figura en <c>appsettings*.json</c>: los códigos son un secreto del entorno de despliegue.
/// </summary>
public sealed class RegistrationOptions
{
    public const string SectionName = "Registration";

    // Un valor no booleano falla al enlazar y el mensaje del binder nombra la clave.
    public bool Enabled { get; set; }

    // Texto para que el validador controle el mensaje y ningún código se repita en un error: lista separada por comas.
    public string? InviteCodes { get; set; }

    /// <summary>
    /// Los códigos de la lista, recortados. Los elementos en blanco se conservan como cadenas vacías para que el validador los
    /// rechace; sin lista devuelve una lista vacía.
    /// </summary>
    public IReadOnlyList<string> ParseInviteCodes()
        => string.IsNullOrWhiteSpace(InviteCodes) ? [] : [.. InviteCodes.Split(',').Select(code => code.Trim())];
}
