using System.Net;

using TimeTracker.Web.Http;
using TimeTracker.Web.Team;
using TimeTracker.Web.Tests.Support;
using TimeTracker.Web.WorkSchedules;

namespace TimeTracker.Web.Tests.WorkSchedules;

public class WorkSchedulesStateTests
{
    private static readonly Guid Oficina = Guid.Parse("a1111111-1111-1111-1111-111111111111");
    private static readonly Guid Luis = Guid.Parse("b2222222-2222-2222-2222-222222222222");

    private sealed class FakeApi : IWorkSchedulesApi
    {
        public List<WorkScheduleDto> Stored { get; } = [];

        public List<WorkScheduleRequestDto> Created { get; } = [];

        public List<(Guid Id, WorkScheduleRequestDto Request)> Updated { get; } = [];

        public List<(Guid Member, Guid? Schedule)> Assigned { get; } = [];

        public ApiFailure? SaveFailure { get; set; }

        public ApiFailure? DeleteFailure { get; set; }

        public ApiFailure? AssignFailure { get; set; }

        public ApiFailure? ListFailure { get; set; }

        public Task<ApiCallResult<WorkScheduleListDto>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(ListFailure is { } failure
                ? new ApiCallResult<WorkScheduleListDto>(null, failure)
                : new ApiCallResult<WorkScheduleListDto>(new WorkScheduleListDto([.. Stored]), null));

        public Task<ApiCallResult<WorkScheduleDto>> CreateAsync(WorkScheduleRequestDto request, CancellationToken cancellationToken = default)
        {
            Created.Add(request);
            return Task.FromResult(SaveFailure is { } failure ? new ApiCallResult<WorkScheduleDto>(null, failure) : new(Add(request), null));
        }

        public Task<ApiCallResult<WorkScheduleDto>> UpdateAsync(Guid id, WorkScheduleRequestDto request, CancellationToken cancellationToken = default)
        {
            Updated.Add((id, request));
            return Task.FromResult(SaveFailure is { } failure ? new ApiCallResult<WorkScheduleDto>(null, failure) : new(Add(request), null));
        }

        public Task<ApiFailure?> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (DeleteFailure is null)
            {
                Stored.RemoveAll(schedule => schedule.Id == id);
            }

            return Task.FromResult(DeleteFailure);
        }

        public Task<ApiFailure?> AssignAsync(Guid memberId, Guid? workScheduleId, CancellationToken cancellationToken = default)
        {
            Assigned.Add((memberId, workScheduleId));
            return Task.FromResult(AssignFailure);
        }

        public Task<ApiCallResult<WorkScheduleDto?>> MineAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException("La página de jornadas nunca consulta la propia.");

        private WorkScheduleDto Add(WorkScheduleRequestDto request)
        {
            var dto = new WorkScheduleDto(Guid.NewGuid(), request.Name, request.StartTime ?? default, request.LunchStart ?? default, request.LunchMinutes ?? 0, request.ExpectedBreakMinutes ?? 0, request.Days, 0);
            Stored.Add(dto);
            return dto;
        }
    }

    private static WorkScheduleDto Existing(int assigned = 0)
        => new(Oficina, "Oficina", new TimeOnly(9, 0), new TimeOnly(14, 0), 60, 30, [DayOfWeek.Monday, DayOfWeek.Friday], assigned);

    private static WorkSchedulesState Filled(FakeApi api, bool isManager = true)
    {
        var state = new WorkSchedulesState(api, isManager)
        {
            Name = " Oficina ",
            StartTime = "09:00",
            LunchStart = "14:00",
            LunchMinutes = 60,
            ExpectedBreakMinutes = 30,
        };
        state.SetDay(DayOfWeek.Monday, true);
        state.SetDay(DayOfWeek.Friday, true);
        return state;
    }

    [Fact]
    public async Task Load_ListsTheSchedules()
    {
        var api = new FakeApi { Stored = { Existing(2) } };
        var state = new WorkSchedulesState(api, true);

        await state.LoadAsync();

        Assert.Equal(2, Assert.Single(state.Items).AssignedCount);
        Assert.Null(state.ErrorMessage);
    }

    [Fact]
    public async Task Load_WithAFailure_ShowsTheGenericMessage_ButNotOnA401()
    {
        var failing = new WorkSchedulesState(new FakeApi { ListFailure = Failures.Of(HttpStatusCode.InternalServerError) }, true);
        var unauthorized = new WorkSchedulesState(new FakeApi { ListFailure = Failures.Of(HttpStatusCode.Unauthorized) }, true);

        await failing.LoadAsync();
        await unauthorized.LoadAsync();

        Assert.Equal(ApiRequest.GenericErrorMessage, failing.ErrorMessage);
        Assert.Null(unauthorized.ErrorMessage);
    }

    [Fact]
    public async Task Submit_Creates_SendingTrimmedNameParsedTimesAndOrderedDays()
    {
        var api = new FakeApi();
        var state = Filled(api);

        await state.SubmitAsync();

        var request = Assert.Single(api.Created);
        Assert.Equal(new WorkScheduleRequestDto("Oficina", new TimeOnly(9, 0), new TimeOnly(14, 0), 60, 30, [DayOfWeek.Monday, DayOfWeek.Friday]), request, new RequestComparer());
        Assert.Equal("Jornada guardada.", state.Notice);
        Assert.Single(state.Items);
        Assert.Equal(string.Empty, state.Name);
        Assert.Empty(state.SelectedDays);
        Assert.Null(state.EditingId);
    }

    private sealed class RequestComparer : IEqualityComparer<WorkScheduleRequestDto>
    {
        public bool Equals(WorkScheduleRequestDto? x, WorkScheduleRequestDto? y)
            => x is not null && y is not null && x.Name == y.Name && x.StartTime == y.StartTime && x.LunchStart == y.LunchStart
                && x.LunchMinutes == y.LunchMinutes && x.ExpectedBreakMinutes == y.ExpectedBreakMinutes && x.Days.SequenceEqual(y.Days);

        public int GetHashCode(WorkScheduleRequestDto obj) => obj.Name.GetHashCode(StringComparison.Ordinal);
    }

    [Fact]
    public async Task Submit_AcceptsTimesWithSeconds_BecauseTheBrowserFieldCanReportThem()
    {
        var api = new FakeApi();
        var state = Filled(api);
        state.StartTime = "09:30:00";
        state.LunchStart = "13:15:00";

        await state.SubmitAsync();

        var request = Assert.Single(api.Created);
        Assert.Equal(new TimeOnly(9, 30), request.StartTime);
        Assert.Equal(new TimeOnly(13, 15), request.LunchStart);
    }

    [Fact]
    public async Task Submit_WithAnUnparseableTime_SendsNullSoTheServerReportsTheField()
    {
        var api = new FakeApi();
        var state = Filled(api);
        state.StartTime = string.Empty;

        await state.SubmitAsync();

        Assert.Null(Assert.Single(api.Created).StartTime);
    }

    [Fact]
    public async Task Edit_FillsTheForm_AndSubmitUpdatesInsteadOfCreating()
    {
        var api = new FakeApi { Stored = { Existing() } };
        var state = new WorkSchedulesState(api, true);
        await state.LoadAsync();

        state.Edit(state.Items[0]);
        state.Name = "Oficina tarde";
        await state.SubmitAsync();

        Assert.Null(state.EditingId);
        Assert.Empty(api.Created);
        var (id, request) = Assert.Single(api.Updated);
        Assert.Equal(Oficina, id);
        Assert.Equal("Oficina tarde", request.Name);
        Assert.Equal(new TimeOnly(9, 0), request.StartTime);
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Friday], request.Days);
    }

    [Fact]
    public void Edit_ThenNew_ResetsTheForm()
    {
        var state = new WorkSchedulesState(new FakeApi(), true);

        state.Edit(Existing());
        Assert.Equal(Oficina, state.EditingId);
        Assert.Equal("09:00", state.StartTime);
        Assert.Equal(60, state.LunchMinutes);

        state.NewSchedule();
        Assert.Null(state.EditingId);
        Assert.Equal(string.Empty, state.Name);
        Assert.Empty(state.SelectedDays);
    }

    [Fact]
    public async Task Submit_WithServerFieldErrors_ShowsThemPerField_AndKeepsTheForm()
    {
        var fields = new Dictionary<string, string[]> { ["name"] = ["El nombre es obligatorio."], ["days"] = ["Seleccione al menos un día."] };
        var api = new FakeApi { SaveFailure = Failures.Of(HttpStatusCode.BadRequest, fields: fields) };
        var state = Filled(api);

        await state.SubmitAsync();

        Assert.Equal(FormFeedback.ReviewFieldsMessage, state.ErrorMessage);
        Assert.Equal(["El nombre es obligatorio."], state.ErrorsFor("name"));
        Assert.Equal(["Seleccione al menos un día."], state.ErrorsFor("days"));
        Assert.Null(state.Notice);
        Assert.Equal(" Oficina ", state.Name);
    }

    [Fact]
    public async Task Submit_ShowsTheConflictDetail_AndForbiddenAsNoPermission()
    {
        var conflict = Filled(new FakeApi { SaveFailure = Failures.Of(HttpStatusCode.Conflict, "Ya existe una jornada con ese nombre.") });
        var forbidden = Filled(new FakeApi { SaveFailure = Failures.Of(HttpStatusCode.Forbidden) });

        await conflict.SubmitAsync();
        await forbidden.SubmitAsync();

        Assert.Equal("Ya existe una jornada con ese nombre.", conflict.ErrorMessage);
        Assert.Equal(WorkSchedulesState.NoPermissionMessage, forbidden.ErrorMessage);
    }

    [Fact]
    public async Task NonManagers_NeverCallTheApi()
    {
        var api = new FakeApi();
        var state = Filled(api, isManager: false);

        await state.SubmitAsync();
        await state.DeleteAsync(Oficina);
        await state.AssignAsync(Luis, Oficina);

        Assert.Empty(api.Created);
        Assert.Empty(api.Assigned);
        Assert.Equal(WorkSchedulesState.NoPermissionMessage, state.ErrorMessage);
    }

    [Fact]
    public async Task Delete_RemovesTheSchedule_OrShowsTheConflictOfAnAssignedOne()
    {
        var api = new FakeApi { Stored = { Existing() } };
        var state = new WorkSchedulesState(api, true);
        await state.LoadAsync();

        await state.DeleteAsync(Oficina);
        Assert.Empty(state.Items);
        Assert.Equal("Jornada borrada.", state.Notice);

        api.Stored.Add(Existing(1));
        api.DeleteFailure = Failures.Of(HttpStatusCode.Conflict, "No se puede borrar: está asignada.");
        await state.LoadAsync();
        await state.DeleteAsync(Oficina);

        Assert.Single(state.Items);
        Assert.Equal("No se puede borrar: está asignada.", state.ErrorMessage);
        Assert.Null(state.Notice);
    }

    [Fact]
    public async Task Assign_UpdatesTheMemberAndReloadsTheCounts_AndUnassignSendsNull()
    {
        var api = new FakeApi { Stored = { Existing() } };
        var state = new WorkSchedulesState(api, true);
        state.SetMembers([new TeamMemberDto(Luis, "Luis Ejemplo")]);
        await state.LoadAsync();

        await state.AssignAsync(Luis, Oficina);
        Assert.Equal((Luis, (Guid?)Oficina), Assert.Single(api.Assigned));
        Assert.Equal(Oficina, Assert.Single(state.Members).WorkScheduleId);
        Assert.Equal("Jornada asignada.", state.Notice);

        await state.AssignAsync(Luis, null);
        Assert.Equal((Luis, (Guid?)null), api.Assigned[1]);
        Assert.Null(Assert.Single(state.Members).WorkScheduleId);
        Assert.Equal("Jornada retirada.", state.Notice);
    }

    [Fact]
    public async Task Assign_WithAFailure_KeepsTheMemberAndShowsTheMessage()
    {
        var api = new FakeApi { AssignFailure = Failures.Of(HttpStatusCode.NotFound) };
        var state = new WorkSchedulesState(api, true);
        state.SetMembers([new TeamMemberDto(Luis, "Luis Ejemplo")]);

        await state.AssignAsync(Luis, Oficina);

        Assert.Null(Assert.Single(state.Members).WorkScheduleId);
        Assert.Equal(WorkSchedulesState.NotFoundMessage, state.ErrorMessage);
    }

    [Fact]
    public void Summary_DescribesNameEntryLunchBreakAndDays()
    {
        var text = WorkSchedulePresentation.Summary(Existing());

        Assert.Equal("Oficina: entrada 09:00, almuerzo 14:00 (60 min), pausa esperada 30 min, lunes, viernes", text);
        Assert.Equal("lunes, domingo", WorkSchedulePresentation.DaysLabel([DayOfWeek.Sunday, DayOfWeek.Monday]));
    }
}
