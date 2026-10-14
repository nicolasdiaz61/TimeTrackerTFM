using TimeTracker.Domain.Users;

namespace TimeTracker.Api.Seeding;

/// <summary>Una persona de demostración. Todos los datos son inventados (dominio <c>example.test</c>).</summary>
internal sealed record DemoUser(string Email, string DisplayName, UserRole Role);

internal static class DemoUsers
{
    public static readonly DemoUser Manager = new("marta.responsable@example.test", "Marta Ejemplo", UserRole.Manager);

    /// <summary>Reportes directos de Marta.</summary>
    public static readonly IReadOnlyList<DemoUser> Reports =
    [
        new("luis.empleado@example.test", "Luis Ejemplo", UserRole.Employee),
        new("ana.empleada@example.test", "Ana Ejemplo", UserRole.Employee),
    ];

    /// <summary>Empleado sin responsable.</summary>
    public static readonly DemoUser Unassigned = new("pablo.empleado@example.test", "Pablo Ejemplo", UserRole.Employee);
}
