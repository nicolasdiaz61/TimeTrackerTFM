namespace TimeTracker.Domain.Users;

/// <summary>
/// Cuenta de usuario: identidad, credencial (solo el hash), rol y responsable directo.
/// </summary>
/// <remarks>
/// Recibe el identificador ya generado y nunca lee el reloj. El constructor privado con parámetros lo usan
/// tanto <see cref="Create"/> como EF Core (enlace por constructor).
/// </remarks>
public sealed class UserAccount
{
    public const int MaxEmailLength = 254;

    public const int MaxDisplayNameLength = 100;

    private UserAccount(Guid id, string email, string displayName, string passwordHash, UserRole role, Guid? managerId)
    {
        Id = id;
        Email = email;
        DisplayName = displayName;
        PasswordHash = passwordHash;
        Role = role;
        ManagerId = managerId;
    }

    public Guid Id { get; }

    public string Email { get; }

    public string DisplayName { get; }

    public string PasswordHash { get; }

    public UserRole Role { get; }

    public Guid? ManagerId { get; }

    public static UserAccount Create(Guid id, string email, string displayName, string passwordHash, UserRole role, Guid? managerId)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("El identificador del usuario no puede estar vacío.", nameof(id));
        }

        var normalizedEmail = RequireValidEmail(email);
        var trimmedName = RequireValidDisplayName(displayName);

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("El hash de la contraseña no puede estar vacío.", nameof(passwordHash));
        }

        if (!Enum.IsDefined(role))
        {
            throw new ArgumentException("El rol no es válido.", nameof(role));
        }

        if (managerId == Guid.Empty || managerId == id)
        {
            throw new ArgumentException("El responsable debe ser otro usuario.", nameof(managerId));
        }

        return new(id, normalizedEmail, trimmedName, passwordHash, role, managerId);
    }

    // Una sola regla de normalización (DD-5): la usan Create, el inicio de sesión y el repositorio.
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string RequireValidEmail(string email)
    {
        var normalized = NormalizeEmail(email);
        if (normalized.Length == 0 || normalized.Length > MaxEmailLength || !normalized.Contains('@', StringComparison.Ordinal))
        {
            throw new ArgumentException("El correo debe ser válido y tener como máximo 254 caracteres.", nameof(email));
        }

        return normalized;
    }

    private static string RequireValidDisplayName(string displayName)
    {
        var trimmed = displayName.Trim();
        if (trimmed.Length == 0 || trimmed.Length > MaxDisplayNameLength)
        {
            throw new ArgumentException("El nombre debe tener entre 1 y 100 caracteres.", nameof(displayName));
        }

        return trimmed;
    }
}
