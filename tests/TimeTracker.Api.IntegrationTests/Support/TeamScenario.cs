using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Domain.TimeTracking;
using TimeTracker.Domain.Users;

namespace TimeTracker.Api.IntegrationTests.Support;

/// <summary>
/// Equipos inventados sobre la base compartida: marta (Manager) con luis y ana; pablo sin responsable; diego (Manager) con carmen.
/// Cada llamada crea usuarios nuevos con correo único, así que las pruebas no se pisan entre sí.
/// </summary>
internal sealed record TeamScenario(UserAccount Marta, UserAccount Luis, UserAccount Ana, UserAccount Pablo, UserAccount Diego, UserAccount Carmen)
{
    public static async Task<TeamScenario> CreateAsync(PostgresFixture postgres)
    {
        var marta = UserSeed.Create("marta", UserRole.Manager, displayName: "Marta Ejemplo");
        var luis = UserSeed.Create("luis", managerId: marta.Id, displayName: "Luis Ejemplo");
        var ana = UserSeed.Create("ana", managerId: marta.Id, displayName: "Ana Ejemplo");
        var pablo = UserSeed.Create("pablo", displayName: "Pablo Ejemplo");
        var diego = UserSeed.Create("diego", UserRole.Manager, displayName: "Diego Ejemplo");
        var carmen = UserSeed.Create("carmen", managerId: diego.Id, displayName: "Carmen Ejemplo");

        await using var context = postgres.CreateContext(postgres.ConnectionString);
        // Los responsables primero: aunque no hay clave foránea, el orden refleja el dominio.
        await UserSeed.AddAsync(context, marta, diego, luis, ana, pablo, carmen);
        return new TeamScenario(marta, luis, ana, pablo, diego, carmen);
    }

    /// <summary>Inserta un fichaje; con <paramref name="minutes"/> nulo queda abierto.</summary>
    public static async Task AddEntryAsync(PostgresFixture postgres, Guid employeeId, DateTimeOffset at, int? minutes)
    {
        var entry = TimeEntry.Start(employeeId, TimeEntryType.Work, at);
        if (minutes is { } worked)
        {
            entry.Close(at.AddMinutes(worked));
        }

        await using var context = postgres.CreateContext(postgres.ConnectionString);
        context.TimeEntries.Add(entry);
        await context.SaveChangesAsync(CancellationToken.None);
    }
}
