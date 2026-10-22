using TimeTracker.Application.DemoData;
using TimeTracker.Domain.Users;

namespace TimeTracker.Api.Seeding;

/// <summary>Una persona de demostración con su perfil de fichajes. Todos los datos son inventados (dominio <c>example.test</c>).</summary>
internal sealed record DemoUser(string Email, string DisplayName, UserRole Role, DemoScheduleProfile Schedule);

internal static class DemoUsers
{
    /// <summary>Dominio de los correos de demostración: el validador de arranque busca usuarios con él cuando el registro está abierto (DD-22).</summary>
    public const string EmailDomain = "@example.test";

    public static readonly DemoUser Manager = new("marta.responsable@example.test", "Marta Ejemplo", UserRole.Manager, DemoScheduleProfiles.Manager);

    /// <summary>Reportes directos de Marta.</summary>
    public static readonly IReadOnlyList<DemoUser> Reports =
    [
        new("luis.empleado@example.test", "Luis Ejemplo", UserRole.Employee, DemoScheduleProfiles.EarlyBird),
        new("ana.empleada@example.test", "Ana Ejemplo", UserRole.Employee, DemoScheduleProfiles.LateShift),
    ];

    /// <summary>Empleado sin responsable.</summary>
    public static readonly DemoUser Unassigned = new("pablo.empleado@example.test", "Pablo Ejemplo", UserRole.Employee, DemoScheduleProfiles.HalfDay);

    public static IEnumerable<DemoUser> All => [Manager, .. Reports, Unassigned];
}
