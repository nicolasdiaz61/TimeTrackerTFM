using Microsoft.AspNetCore.Identity;

using Npgsql;

using TimeTracker.Domain.Companies;
using TimeTracker.Domain.Users;
using TimeTracker.Infrastructure.Persistence;

namespace TimeTracker.Api.IntegrationTests.Support;

/// <summary>Crea usuarios de prueba con correo único y siembra filas que <see cref="UserAccount.Create"/> rechazaría.</summary>
internal static class UserSeed
{
    public const string Password = "clave-de-prueba-123";

    // PBKDF2 es lento a propósito: se calcula una sola vez y se comparte entre todas las pruebas.
    private static readonly Lazy<string> SharedHash = new(() => new PasswordHasher<object>().HashPassword(new object(), Password));

    public static string PasswordHash => SharedHash.Value;

    public static UserAccount Create(string name = "usuario", UserRole role = UserRole.Employee, Guid? managerId = null, string? displayName = null)
        => UserAccount.Create(Guid.NewGuid(), Company.DefaultId, $"{name}.{Guid.NewGuid():N}@example.test", displayName ?? name, PasswordHash, role, managerId);

    public static async Task AddAsync(TimeTrackerDbContext context, params UserAccount[] users)
    {
        context.Users.AddRange(users);
        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Inserta una fila por SQL sin pasar por el dominio (restricciones, hash ilegible).</summary>
    public static async Task InsertRawAsync(
        string connectionString,
        Guid id,
        string email,
        string role = "Employee",
        Guid? managerId = null,
        string? passwordHash = null,
        Guid? companyId = null)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand(
            "INSERT INTO users (id, company_id, email, display_name, password_hash, role, manager_id) VALUES (@id, @company, @email, @name, @hash, @role, @manager)",
            connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("company", companyId ?? Company.DefaultId);
        command.Parameters.AddWithValue("email", email);
        command.Parameters.AddWithValue("name", "Usuario de prueba");
        command.Parameters.AddWithValue("hash", passwordHash ?? PasswordHash);
        command.Parameters.AddWithValue("role", role);
        command.Parameters.AddWithValue("manager", (object?)managerId ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }
}
