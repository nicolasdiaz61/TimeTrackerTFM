using System.Text.Json;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Time.Testing;

using TimeTracker.Application.Team;
using TimeTracker.Application.Tests.Fakes;
using TimeTracker.Application.Time;
using TimeTracker.Application.TimeTracking;
using TimeTracker.Application.WorkSchedules;
using TimeTracker.Domain.Companies;
using TimeTracker.Domain.TimeTracking;
using TimeTracker.Domain.Users;
using TimeTracker.Domain.WorkSchedules;
using TimeTracker.Infrastructure.Assistant;
using TimeTracker.Infrastructure.Tests.Fakes;

namespace TimeTracker.Infrastructure.Tests.Assistant;

/// <summary>Monta el conjunto de herramientas con dobles en memoria, un reloj fijo (domingo 2026-10-18, 12:00 local) y un registrador de captura.</summary>
internal sealed class ToolHarness
{
    public static readonly Guid Marta = Guid.Parse("00000000-0000-4000-8000-00000000000a");
    public static readonly Guid Ana = Guid.Parse("00000000-0000-4000-8000-00000000000b");
    public static readonly Guid Luis = Guid.Parse("00000000-0000-4000-8000-00000000000c");
    public static readonly Guid Pablo = Guid.Parse("00000000-0000-4000-8000-00000000000d");
    public static readonly DateOnly Today = new(2026, 10, 18);

    public ToolHarness(Guid userId, UserRole role, ITimeEntryRepository? entries = null)
    {
        Schedules = new InMemoryWorkScheduleRepository(Users);
        Users.Seed(
            UserAccount.Create(Marta, Company.DefaultId, "marta@example.test", "Marta Responsable", "hash", UserRole.Manager, null),
            UserAccount.Create(Ana, Company.DefaultId, "ana@example.test", "Ana Empleada", "hash", UserRole.Employee, Marta),
            UserAccount.Create(Luis, Company.DefaultId, "luis@example.test", "Luis Empleado", "hash", UserRole.Employee, Marta),
            UserAccount.Create(Pablo, Company.DefaultId, "pablo@example.test", "Pablo Ajeno", "hash", UserRole.Employee, Guid.Parse("00000000-0000-4000-8000-0000000000ff")));
        Clock.SetUtcNow(SpainTime.ToUtc(Today, new TimeOnly(12, 0)));
        var currentUser = new FixedCurrentUser(userId, role);
        var repository = entries ?? Entries;
        Toolset = new AssistantToolset(
            new GetTimeEntriesUseCase(repository, currentUser),
            new GetTeamMembersUseCase(Users, currentUser),
            new GetTeamMemberTimeEntriesUseCase(Users, repository, currentUser),
            new GetTeamSummaryUseCase(Users, repository, Schedules, currentUser),
            new GetMyWorkScheduleUseCase(Users, Schedules, currentUser),
            new ListWorkSchedulesUseCase(Schedules),
            currentUser,
            Clock,
            Logger);
        Tools = Toolset.CreateTools();
    }

    public InMemoryTimeEntryRepository Entries { get; } = new();

    public InMemoryUserRepository Users { get; } = new();

    public InMemoryWorkScheduleRepository Schedules { get; }

    public FakeTimeProvider Clock { get; } = new();

    public CapturingLogger<AssistantToolset> Logger { get; } = new();

    public AssistantToolset Toolset { get; }

    public IReadOnlyList<AIFunction> Tools { get; }

    /// <summary>Añade un intervalo cerrado entre dos horas locales de España del mismo día.</summary>
    public void SeedEntry(Guid employeeId, DateOnly date, TimeOnly from, TimeOnly to, TimeEntryType type = TimeEntryType.Work)
    {
        var entry = TimeEntry.Start(Company.DefaultId, employeeId, type, SpainTime.ToUtc(date, from));
        entry.Close(SpainTime.ToUtc(date, to));
        Entries.Seed(entry);
    }

    /// <summary>Crea una jornada de la compañía por defecto y, si se indica, la asigna a una persona.</summary>
    public WorkSchedule SeedSchedule(string name, IReadOnlyCollection<DayOfWeek> days, params Guid[] assignTo)
    {
        var schedule = WorkSchedule.Create(
            Guid.NewGuid(),
            Company.DefaultId,
            new WorkScheduleDetails(name, new TimeOnly(8, 30), new TimeOnly(14, 0), 45, 20, days));
        Schedules.Seed(schedule);
        foreach (var person in assignTo)
        {
            Users.All.Single(user => user.Id == person).AssignWorkSchedule(schedule.Id);
        }

        return schedule;
    }

    public async Task<JsonElement> InvokeAsync(string tool, params (string Name, object? Value)[] arguments)
    {
        var function = Tools.Single(candidate => candidate.Name == tool);
        var args = new AIFunctionArguments();
        foreach (var (name, value) in arguments)
        {
            args[name] = value;
        }

        var result = await function.InvokeAsync(args);
        return JsonSerializer.SerializeToElement(result, function.JsonSerializerOptions);
    }
}
