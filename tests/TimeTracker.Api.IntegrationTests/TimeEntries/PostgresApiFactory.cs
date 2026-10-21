using Microsoft.AspNetCore.Hosting;

using TimeTracker.Api.IntegrationTests.Support;

namespace TimeTracker.Api.IntegrationTests.TimeEntries;

/// <summary>
/// API sobre el PostgreSQL real del fixture, con una identidad nueva por instancia (sin limpiar la base) y el
/// reloj controlable de <see cref="ApiFactory"/>. El cliente por defecto lleva el token de <see cref="EmployeeId"/>.
/// Dos instancias con distinto <see cref="EmployeeId"/> comparten la misma base.
/// </summary>
public sealed class PostgresApiFactory : ApiFactory
{
    private readonly string _connectionString;
    private readonly string _role;

    /// <summary>Crea también la fila del usuario del token (la clave foránea de los fichajes exige que el empleado exista).</summary>
    public PostgresApiFactory(string connectionString, Guid? employeeId = null, string role = "Employee")
    {
        _connectionString = connectionString;
        _role = role;
        EmployeeId = employeeId ?? Guid.NewGuid();
        UserSeed.EnsureExists(connectionString, EmployeeId, role);
    }

    public Guid EmployeeId { get; }

    /// <summary>Garantiza antes la fila del usuario del token: la clave foránea de los fichajes exige que exista.</summary>
    public override HttpClient CreateClientFor(Guid userId, string role)
    {
        // Un rol desconocido (prueba de token inválido) no puede sembrarse: la CHECK de roles lo rechazaría.
        if (role is "Employee" or "Manager")
        {
            UserSeed.EnsureExists(_connectionString, userId, role);
        }

        return base.CreateClientFor(userId, role);
    }

    protected override Guid AuthenticatedUserId => EmployeeId;

    protected override string AuthenticatedRole => _role;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("ConnectionStrings:Default", _connectionString);
    }
}
