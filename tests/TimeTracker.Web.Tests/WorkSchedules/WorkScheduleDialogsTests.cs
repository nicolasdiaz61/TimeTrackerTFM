using System.Net;

using TimeTracker.Web.Http;
using TimeTracker.Web.WorkSchedules;

namespace TimeTracker.Web.Tests.WorkSchedules;

public class WorkScheduleDialogsTests
{
    private static readonly Guid Oficina = Guid.Parse("a1111111-1111-1111-1111-111111111111");

    private sealed class FakeApi : IWorkSchedulesApi
    {
        public List<WorkScheduleDto> Stored { get; } = [new(Oficina, "Oficina", new TimeOnly(9, 0), new TimeOnly(14, 0), 60, 30, [DayOfWeek.Monday], 0)];

        public ApiFailure? SaveFailure { get; set; }

        public ApiFailure? DeleteFailure { get; set; }

        public Task<ApiCallResult<WorkScheduleListDto>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new ApiCallResult<WorkScheduleListDto>(new WorkScheduleListDto([.. Stored]), null));

        public Task<ApiCallResult<WorkScheduleDto>> CreateAsync(WorkScheduleRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(SaveFailure is { } failure ? new ApiCallResult<WorkScheduleDto>(null, failure) : new(Stored[0], null));

        public Task<ApiCallResult<WorkScheduleDto>> UpdateAsync(Guid id, WorkScheduleRequestDto request, CancellationToken cancellationToken = default)
            => CreateAsync(request, cancellationToken);

        public Task<ApiFailure?> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(DeleteFailure);

        public Task<ApiFailure?> AssignAsync(Guid memberId, Guid? workScheduleId, CancellationToken cancellationToken = default)
            => Task.FromResult<ApiFailure?>(null);

        public Task<ApiCallResult<WorkScheduleDto?>> MineAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private static ApiFailure Failure(HttpStatusCode status)
        => new(new ApiRequestException("Error.", status), new Dictionary<string, string[]>(), null);

    private static async Task<(WorkScheduleDialogs Dialogs, WorkSchedulesState State, FakeApi Api)> ArrangeAsync()
    {
        var api = new FakeApi();
        var state = new WorkSchedulesState(api, isManager: true);
        await state.LoadAsync();
        return (new WorkScheduleDialogs(state), state, api);
    }

    [Fact]
    public async Task StartsClosed()
    {
        var (dialogs, _, _) = await ArrangeAsync();

        Assert.Equal(WorkScheduleDialog.None, dialogs.Current);
    }

    [Fact]
    public async Task OpenCreate_ShowsAnEmptyFormTitledNuevaJornada()
    {
        var (dialogs, state, _) = await ArrangeAsync();
        state.Name = "Sobra";

        dialogs.OpenCreate();

        Assert.Equal(WorkScheduleDialog.Form, dialogs.Current);
        Assert.Equal("Nueva jornada", dialogs.FormTitle);
        Assert.False(dialogs.IsEditing);
        Assert.Equal(string.Empty, state.Name);
    }

    [Fact]
    public async Task OpenEdit_PrefillsTheSameFormTitledEditarJornada()
    {
        var (dialogs, state, _) = await ArrangeAsync();

        dialogs.OpenEdit(state.Items[0]);

        Assert.Equal(WorkScheduleDialog.Form, dialogs.Current);
        Assert.Equal("Editar jornada", dialogs.FormTitle);
        Assert.True(dialogs.IsEditing);
        Assert.Equal("Oficina", state.Name);
        Assert.Equal("09:00", state.StartTime);
    }

    [Fact]
    public async Task Close_FromTheFormClearsItAndHidesTheDialog()
    {
        var (dialogs, state, _) = await ArrangeAsync();
        dialogs.OpenEdit(state.Items[0]);

        dialogs.Close();

        Assert.Equal(WorkScheduleDialog.None, dialogs.Current);
        Assert.Null(state.EditingId);
        Assert.Equal(string.Empty, state.Name);
    }

    [Fact]
    public async Task OpenCreate_AfterAFailedSaveDropsTheOldError()
    {
        var (dialogs, state, api) = await ArrangeAsync();
        api.SaveFailure = Failure(HttpStatusCode.Forbidden);
        dialogs.OpenCreate();
        await dialogs.SubmitAsync();
        Assert.NotNull(state.ErrorMessage);

        dialogs.Close();
        dialogs.OpenCreate();

        Assert.Null(state.ErrorMessage);
    }

    [Fact]
    public async Task Submit_ClosesTheDialogWhenSavedAndKeepsItOpenOnError()
    {
        var (dialogs, state, api) = await ArrangeAsync();
        dialogs.OpenEdit(state.Items[0]);
        await dialogs.SubmitAsync();
        Assert.Equal(WorkScheduleDialog.None, dialogs.Current);
        Assert.Equal("Jornada guardada.", state.Notice);

        api.SaveFailure = Failure(HttpStatusCode.Forbidden);
        dialogs.OpenEdit(state.Items[0]);
        await dialogs.SubmitAsync();

        Assert.Equal(WorkScheduleDialog.Form, dialogs.Current);
        Assert.NotNull(state.ErrorMessage);
    }

    [Fact]
    public async Task RequestDelete_AsksForConfirmationAndCancelDoesNotDelete()
    {
        var (dialogs, state, api) = await ArrangeAsync();

        dialogs.RequestDelete(state.Items[0]);

        Assert.Equal(WorkScheduleDialog.ConfirmDelete, dialogs.Current);
        Assert.Equal(Oficina, dialogs.PendingDelete?.Id);

        dialogs.Close();

        Assert.Equal(WorkScheduleDialog.None, dialogs.Current);
        Assert.Null(dialogs.PendingDelete);
        Assert.Single(api.Stored);
    }

    [Fact]
    public async Task ConfirmDelete_DeletesAndClosesEvenWhenItFails()
    {
        var (dialogs, state, api) = await ArrangeAsync();
        dialogs.RequestDelete(state.Items[0]);
        await dialogs.ConfirmDeleteAsync();
        Assert.Equal("Jornada borrada.", state.Notice);
        Assert.Equal(WorkScheduleDialog.None, dialogs.Current);

        api.DeleteFailure = Failure(HttpStatusCode.Conflict);
        dialogs.RequestDelete(state.Items[0]);
        await dialogs.ConfirmDeleteAsync();

        Assert.Equal(WorkScheduleDialog.None, dialogs.Current);
        Assert.NotNull(state.ErrorMessage);
    }
}
