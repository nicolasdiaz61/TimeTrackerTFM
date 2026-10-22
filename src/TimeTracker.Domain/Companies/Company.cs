namespace TimeTracker.Domain.Companies;

/// <summary>
/// Compañía (inquilino): agrupa a las personas y los datos que comparten una misma instancia y base de datos.
/// </summary>
/// <remarks>
/// Recibe el identificador y el instante ya generados y nunca lee el reloj. El constructor privado con parámetros lo
/// usan tanto <see cref="Create"/> como EF Core (enlace por constructor). La normalización del nombre vive solo en
/// <see cref="NormalizeName"/> (una regla, como <c>UserAccount.NormalizeEmail</c>).
/// </remarks>
public sealed class Company
{
    public const int MaxNameLength = 100;

    /// <summary>Nombre de la compañía por defecto, que es también la de demostración (ASCII, para coincidir con el SQL de la migración).</summary>
    public const string DefaultName = "Demo";

    /// <summary>Identificador fijo de la compañía por defecto: el mismo valor en todos los entornos y en la migración.</summary>
    public static readonly Guid DefaultId = new("00000000-0000-7000-8000-000000000001");

    private Company(Guid id, string name, string nameNormalized, CompanyStatus status, bool usesPlatformAssistant, DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        NameNormalized = nameNormalized;
        Status = status;
        UsesPlatformAssistant = usesPlatformAssistant;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }

    public string Name { get; }

    public string NameNormalized { get; }

    public CompanyStatus Status { get; }

    /// <summary>Indica si la compañía puede usar la clave de IA de plataforma; solo la compañía por defecto nace con <c>true</c>.</summary>
    public bool UsesPlatformAssistant { get; }

    public DateTimeOffset CreatedAt { get; }

    public static Company Create(Guid id, string name, DateTimeOffset createdAt, bool usesPlatformAssistant = false)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("El identificador de la compañía no puede estar vacío.", nameof(id));
        }

        if (!IsValidName(name))
        {
            throw new ArgumentException("El nombre de la compañía debe tener entre 1 y 100 caracteres.", nameof(name));
        }

        var trimmed = name.Trim();

        if (createdAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("El instante debe estar en UTC (desfase cero).", nameof(createdAt));
        }

        return new(id, trimmed, NormalizeName(trimmed), CompanyStatus.Active, usesPlatformAssistant, createdAt);
    }

    /// <summary>Indica si el nombre cumple la regla de <see cref="Create"/> (de 1 a 100 caracteres tras recortar), para validar antes de crear.</summary>
    public static bool IsValidName(string name) => name.Trim().Length is >= 1 and <= MaxNameLength;

    // Recorta, colapsa los espacios internos y pasa a minúsculas invariantes; no pliega acentos ni aplica FormKC (decisión del usuario).
    public static string NormalizeName(string name)
        => string.Join(' ', name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
}
