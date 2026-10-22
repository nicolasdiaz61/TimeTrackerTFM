using TimeTracker.Application.Team;
using TimeTracker.Application.Tests.Fakes;
using TimeTracker.Domain.Companies;
using TimeTracker.Domain.Users;

namespace TimeTracker.Application.Tests;

public class GetTeamMembersUseCaseTests
{
    private static readonly Guid MartaId = Guid.Parse("0197a1b2-6c3d-7e4f-8a5b-6c7d8e9f0a60");

    private readonly InMemoryUserRepository _users = new();

    private GetTeamMembersUseCase CreateUseCase(Guid currentUserId) => new(_users, new FixedCurrentUser(currentUserId));

    private static UserAccount NewUser(string name, Guid? managerId, Guid? id = null)
        => UserAccount.Create(id ?? Guid.NewGuid(), Company.DefaultId, $"{Guid.NewGuid():N}@example.test", name, "hash-de-prueba", UserRole.Employee, managerId);

    [Fact]
    public async Task Get_ReturnsOnlyDirectReportsOfTheCurrentUser()
    {
        var luis = NewUser("Luis Empleado", MartaId);
        var ana = NewUser("Ana Empleada", MartaId);
        var pablo = NewUser("Pablo Empleado", null);
        var otherManager = NewUser("Otro Responsable", null);
        var otherReport = NewUser("Elena Ajena", otherManager.Id);
        _users.Seed(luis, ana, pablo, otherManager, otherReport);

        var result = await CreateUseCase(MartaId).ExecuteAsync(CancellationToken.None);

        Assert.Equal([new TeamMemberResult(ana.Id, "Ana Empleada"), new TeamMemberResult(luis.Id, "Luis Empleado")], result);
    }

    [Fact]
    public async Task Get_DoesNotIncludeTransitiveReports()
    {
        var diego = NewUser("Diego Empleado", MartaId);
        var carmen = NewUser("Carmen Empleada", diego.Id);
        _users.Seed(diego, carmen);

        var result = await CreateUseCase(MartaId).ExecuteAsync(CancellationToken.None);

        Assert.Equal([new TeamMemberResult(diego.Id, "Diego Empleado")], result);
    }

    [Fact]
    public async Task Get_WithoutReports_ReturnsEmpty()
    {
        _users.Seed(NewUser("Pablo Empleado", null));

        var result = await CreateUseCase(MartaId).ExecuteAsync(CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Get_With101Reports_RequestsLimit100AndReturns100()
    {
        for (var index = 0; index < 101; index++)
        {
            _users.Seed(NewUser($"Miembro {index:D3}", MartaId));
        }

        var result = await CreateUseCase(MartaId).ExecuteAsync(CancellationToken.None);

        Assert.Equal(100, _users.LastLimit);
        Assert.Equal(100, result.Count);
        Assert.Equal(100, GetTeamMembersUseCase.MaxMembers);
    }
}
