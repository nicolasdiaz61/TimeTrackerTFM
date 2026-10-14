using Microsoft.AspNetCore.Hosting;

namespace TimeTracker.Api.IntegrationTests.TimeEntries;

/// <summary>
/// API sobre el PostgreSQL real del fixture, con una identidad nueva por instancia (sin limpiar la base) y el
/// reloj controlable de <see cref="ApiFactory"/>. El cliente por defecto lleva el token de <see cref="EmployeeId"/>.
/// Dos instancias con distinto <see cref="EmployeeId"/> comparten la misma base.
/// </summary>
public sealed class PostgresApiFactory(string connectionString, Guid? employeeId = null, string role = "Employee") : ApiFactory
{
    public Guid EmployeeId { get; } = employeeId ?? Guid.NewGuid();

    protected override Guid AuthenticatedUserId => EmployeeId;

    protected override string AuthenticatedRole => role;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("ConnectionStrings:Default", connectionString);
    }
}
