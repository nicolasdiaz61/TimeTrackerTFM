using System.Net;
using System.Text.Json;

using TimeTracker.Web.Tests.Support;
using TimeTracker.Web.WorkSchedules;

namespace TimeTracker.Web.Tests.WorkSchedules;

public class WorkSchedulesApiClientTests
{
    private static readonly Guid Id = Guid.Parse("a1111111-1111-1111-1111-111111111111");
    private static readonly Guid Luis = Guid.Parse("b2222222-2222-2222-2222-222222222222");

    private const string ScheduleJson = """
        {"id":"a1111111-1111-1111-1111-111111111111","name":"Oficina","startTime":"09:00:00","lunchStart":"14:00:00","lunchMinutes":60,"expectedBreakMinutes":30,"days":[1,2,3,4,5],"assignedCount":2}
        """;

    private static WorkSchedulesApiClient ClientFor(StubHandler handler)
        => new(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test/") });

    private static WorkScheduleRequestDto Request()
        => new("Oficina", new TimeOnly(9, 0), new TimeOnly(14, 0), 60, 30, [DayOfWeek.Monday, DayOfWeek.Friday]);

    [Fact]
    public async Task List_GetsTheSchedulesAndReadsTheContract()
    {
        var handler = new StubHandler(_ => Responses.Json(HttpStatusCode.OK, $$"""{"items":[{{ScheduleJson}}]}"""));

        var result = await ClientFor(handler).ListAsync();

        Assert.Null(result.Error);
        var schedule = Assert.Single(result.Value!.Items);
        Assert.Equal(new TimeOnly(9, 0), schedule.StartTime);
        Assert.Equal(60, schedule.LunchMinutes);
        Assert.Equal(2, schedule.AssignedCount);
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday], schedule.Days);
        Assert.Equal(HttpMethod.Get, handler.Request!.Method);
        Assert.Equal("https://api.example.test/api/work-schedules", handler.Request.RequestUri!.ToString());
    }

    [Fact]
    public async Task Create_PostsTheContractFields_WithNoCompanyIdentifier()
    {
        var handler = new StubHandler(_ => Responses.Json(HttpStatusCode.Created, ScheduleJson));

        var result = await ClientFor(handler).CreateAsync(Request());

        Assert.Null(result.Error);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        using var body = JsonDocument.Parse(handler.Body!);
        Assert.Equal(
            ["days", "endTime", "expectedBreakMinutes", "lunchMinutes", "lunchStart", "name", "startTime"],
            body.RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal("09:00:00", body.RootElement.GetProperty("startTime").GetString());
        Assert.Equal([1, 5], body.RootElement.GetProperty("days").EnumerateArray().Select(day => day.GetInt32()));
        Assert.DoesNotContain("ompan", handler.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Update_PutsToTheScheduleRoute()
    {
        var handler = new StubHandler(_ => Responses.Json(HttpStatusCode.OK, ScheduleJson));

        await ClientFor(handler).UpdateAsync(Id, Request());

        Assert.Equal(HttpMethod.Put, handler.Request!.Method);
        Assert.Equal("https://api.example.test/api/work-schedules/a1111111-1111-1111-1111-111111111111", handler.Request.RequestUri!.ToString());
    }

    [Fact]
    public async Task Save_ReadsTheFieldErrorsAndTheConflictDetail()
    {
        var invalid = await ClientFor(new StubHandler(_ => Responses.Validation("name", "El nombre es obligatorio."))).CreateAsync(Request());
        var conflict = await ClientFor(new StubHandler(_ => Responses.Problem(HttpStatusCode.Conflict, "Ya existe una jornada con ese nombre."))).CreateAsync(Request());

        Assert.Equal(["El nombre es obligatorio."], invalid.Error!.FieldErrors["name"]);
        Assert.Equal("Ya existe una jornada con ese nombre.", conflict.Error!.Message);
    }

    [Fact]
    public async Task Delete_SendsDeleteAndReturnsNullOnSuccess_OrTheConflictDetail()
    {
        var okHandler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var conflictHandler = new StubHandler(_ => Responses.Problem(HttpStatusCode.Conflict, "La jornada está asignada."));

        Assert.Null(await ClientFor(okHandler).DeleteAsync(Id));
        var failure = await ClientFor(conflictHandler).DeleteAsync(Id);

        Assert.Equal(HttpMethod.Delete, okHandler.Request!.Method);
        Assert.Equal(HttpStatusCode.Conflict, failure!.StatusCode);
        Assert.Equal("La jornada está asignada.", failure.Message);
    }

    [Fact]
    public async Task Assign_PutsTheScheduleIdToTheMemberRoute_AndANullRemovesIt()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = ClientFor(handler);

        Assert.Null(await client.AssignAsync(Luis, Id));
        Assert.Equal(HttpMethod.Put, handler.Request!.Method);
        Assert.Equal("https://api.example.test/api/team/members/b2222222-2222-2222-2222-222222222222/work-schedule", handler.Request.RequestUri!.ToString());
        using (var body = JsonDocument.Parse(handler.Body!))
        {
            Assert.Equal(Id, body.RootElement.GetProperty("workScheduleId").GetGuid());
        }

        Assert.Null(await client.AssignAsync(Luis, null));
        using var cleared = JsonDocument.Parse(handler.Body!);
        Assert.Equal(JsonValueKind.Null, cleared.RootElement.GetProperty("workScheduleId").ValueKind);
    }

    [Fact]
    public async Task Mine_ReturnsTheSchedule_OrNullWithoutErrorOn204_OrTheFailure()
    {
        var withSchedule = await ClientFor(new StubHandler(_ => Responses.Json(HttpStatusCode.OK, ScheduleJson))).MineAsync();
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var without = await ClientFor(handler).MineAsync();
        var failed = await ClientFor(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError))).MineAsync();

        Assert.Equal("Oficina", withSchedule.Value!.Name);
        Assert.Null(without.Value);
        Assert.Null(without.Error);
        Assert.Equal("https://api.example.test/api/work-schedules/mine", handler.Request!.RequestUri!.ToString());
        Assert.NotNull(failed.Error);
    }
}
