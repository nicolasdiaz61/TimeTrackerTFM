using System.Net;

using TimeTracker.Web.Http;
using TimeTracker.Web.Team;
using TimeTracker.Web.Tests.Support;

namespace TimeTracker.Web.Tests.Team;

public class TeamMemberCreationFormTests
{
    private const string Temporary = "ClaveTemporal2468Xy";

    private static readonly Guid Ana = Guid.Parse("a1111111-1111-1111-1111-111111111111");
    private static readonly Guid Luis = Guid.Parse("b2222222-2222-2222-2222-222222222222");

    private sealed class FakeApi(Func<CreateEmployeeRequestDto, ApiCallResult<CreateEmployeeResponseDto>> respond) : IEmployeesApi
    {
        public List<CreateEmployeeRequestDto> Requests { get; } = [];

        public Task<ApiCallResult<CreateEmployeeResponseDto>> CreateAsync(CreateEmployeeRequestDto request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }

        public Task<ApiCallResult<TemporaryPasswordResponseDto>> RegenerateTemporaryPasswordAsync(Guid memberId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("El alta nunca regenera.");
    }

    private static ApiCallResult<CreateEmployeeResponseDto> Created()
        => new(new CreateEmployeeResponseDto(Guid.NewGuid(), "Pablo Ejemplo", "pablo@example.test", "Employee", Temporary), null);

    private static ApiCallResult<CreateEmployeeResponseDto> Failed(HttpStatusCode? status, string message = "Mensaje.", IReadOnlyDictionary<string, string[]>? fields = null, int? retryAfter = null)
        => new(null, Failures.Of(status, message, fields, retryAfter));

    private static TeamMemberCreationForm Filled(IEmployeesApi api, bool isManager = true)
    {
        var form = new TeamMemberCreationForm(api, isManager)
        {
            DisplayName = " Pablo Ejemplo ",
            Email = " pablo@example.test ",
        };
        form.SetCandidates([new TeamMemberDto(Ana, "Ana Ejemplo"), new TeamMemberDto(Luis, "Luis Ejemplo")]);
        return form;
    }

    [Fact]
    public async Task Submit_SendsTheTrimmedNameEmailAndRole_AndNoManagerByDefault()
    {
        var api = new FakeApi(_ => Created());
        var form = Filled(api);

        await form.SubmitAsync();

        Assert.Equal(new CreateEmployeeRequestDto("Pablo Ejemplo", "pablo@example.test", "Employee", null), api.Requests.Single());
    }

    [Fact]
    public async Task Submit_SendsTheChosenRoleAndTheManagerFromTheTeamList()
    {
        var api = new FakeApi(_ => Created());
        var form = Filled(api);
        form.Role = "Manager";
        form.SelectManager(Luis);

        await form.SubmitAsync();

        Assert.Equal(new CreateEmployeeRequestDto("Pablo Ejemplo", "pablo@example.test", "Manager", Luis), api.Requests.Single());
    }

    [Fact]
    public async Task AManagerThatIsNotInTheTeamList_IsNeverSent()
    {
        var api = new FakeApi(_ => Created());
        var form = Filled(api);

        form.SelectManager(Guid.NewGuid());
        await form.SubmitAsync();

        Assert.Null(form.ManagerId);
        Assert.Null(api.Requests.Single().ManagerId);
    }

    [Fact]
    public void Candidates_AreExactlyTheTeamMembersReceived()
    {
        var form = Filled(new FakeApi(_ => Created()));

        Assert.Equal([Ana, Luis], form.Candidates.Select(candidate => candidate.Id));
    }

    [Fact]
    public async Task Created_ShowsTheTemporaryPasswordOnceWithTheNotice_AndEmptiesTheForm()
    {
        var form = Filled(new FakeApi(_ => Created()));
        form.SelectManager(Ana);

        await form.SubmitAsync();

        Assert.Equal(Temporary, form.Reveal.Value);
        Assert.Equal("Pablo Ejemplo", form.Reveal.PersonName);
        Assert.Contains("no se volverá a mostrar", TemporaryPasswordReveal.Notice, StringComparison.Ordinal);
        Assert.Contains("ahora", TemporaryPasswordReveal.Notice, StringComparison.Ordinal);
        Assert.Contains("primer inicio de sesión", TemporaryPasswordReveal.Notice, StringComparison.Ordinal);
        Assert.Equal(string.Empty, form.DisplayName);
        Assert.Equal(string.Empty, form.Email);
        Assert.Null(form.ManagerId);
        Assert.Null(form.ErrorMessage);
    }

    [Fact]
    public async Task Dismissing_RemovesThePasswordFromTheState()
    {
        var form = Filled(new FakeApi(_ => Created()));
        await form.SubmitAsync();

        form.Reveal.Dismiss();

        Assert.Null(form.Reveal.Value);
        Assert.Null(form.Reveal.PersonName);
        Assert.False(form.Reveal.IsVisible);
    }

    [Fact]
    public async Task ANewSubmit_RemovesThePreviousPasswordBeforeSending()
    {
        var calls = 0;
        var form = Filled(new FakeApi(_ => ++calls == 1 ? Created() : Failed(HttpStatusCode.Conflict, "Correo repetido.")));
        await form.SubmitAsync();
        form.DisplayName = "Otra Persona";
        form.Email = "otra@example.test";

        await form.SubmitAsync();

        Assert.Null(form.Reveal.Value);
        Assert.Equal("Correo repetido.", form.ErrorMessage);
    }

    [Fact]
    public async Task BadRequest_ShowsTheFieldErrorsNextToTheirFields_AndKeepsTheData()
    {
        var fields = new Dictionary<string, string[]> { ["email"] = ["El correo electrónico debe ser válido."], ["managerId"] = ["El responsable debe ser un Manager de su compañía."] };
        var form = Filled(new FakeApi(_ => Failed(HttpStatusCode.BadRequest, fields: fields)));

        await form.SubmitAsync();

        Assert.Equal(["El correo electrónico debe ser válido."], form.ErrorsFor("email"));
        Assert.Equal(["El responsable debe ser un Manager de su compañía."], form.ErrorsFor("managerId"));
        Assert.Equal(FormFeedback.ReviewFieldsMessage, form.ErrorMessage);
        Assert.Equal(" pablo@example.test ", form.Email);
        Assert.Null(form.Reveal.Value);
    }

    [Theory]
    [InlineData("Ya existe una persona con ese correo electrónico.")]
    [InlineData("Se ha alcanzado el número máximo de personas de la compañía.")]
    public async Task Conflict_ShowsTheApiDetail_AndKeepsTheScreenOperational(string detail)
    {
        var form = Filled(new FakeApi(_ => Failed(HttpStatusCode.Conflict, detail)));

        await form.SubmitAsync();

        Assert.Equal(detail, form.ErrorMessage);
        Assert.Null(form.Reveal.Value);
        Assert.False(form.IsBusy);
        Assert.Equal(" Pablo Ejemplo ", form.DisplayName);
    }

    [Fact]
    public async Task Forbidden_ShowsAPermissionMessage()
    {
        var form = Filled(new FakeApi(_ => Failed(HttpStatusCode.Forbidden)));

        await form.SubmitAsync();

        Assert.Equal(TeamMemberCreationForm.NoPermissionMessage, form.ErrorMessage);
    }

    [Fact]
    public async Task Unauthorized_ShowsNoMessage_BecauseTheHandlerRedirects()
    {
        var form = Filled(new FakeApi(_ => Failed(HttpStatusCode.Unauthorized)));

        await form.SubmitAsync();

        Assert.Null(form.ErrorMessage);
    }

    [Fact]
    public async Task TooManyRequests_AsksToWait()
    {
        var form = Filled(new FakeApi(_ => Failed(HttpStatusCode.TooManyRequests, retryAfter: 20)));

        await form.SubmitAsync();

        Assert.StartsWith(TeamMemberCreationForm.TooManyRequestsMessage, form.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("20 segundos", form.ErrorMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(null)]
    public async Task OtherFailures_ShowTheGenericMessage(HttpStatusCode? status)
    {
        var form = Filled(new FakeApi(_ => Failed(status, "System.Exception")));

        await form.SubmitAsync();

        Assert.Equal(ApiRequest.GenericErrorMessage, form.ErrorMessage);
    }

    [Fact]
    public async Task AnEmployee_SeesNoPermission_AndTheApiIsNeverCalled()
    {
        var api = new FakeApi(_ => Created());
        var form = Filled(api, isManager: false);

        await form.SubmitAsync();

        Assert.Empty(api.Requests);
        Assert.Equal(TeamMemberCreationForm.NoPermissionMessage, form.ErrorMessage);
        Assert.Null(form.Reveal.Value);
    }

    [Fact]
    public void TheForm_OffersOnlyCreation_WithNoRemovalOrEditionMembers()
    {
        var members = typeof(TeamMemberCreationForm).GetMembers().Select(member => member.Name).ToList();

        Assert.DoesNotContain(members, name => name.Contains("Delete", StringComparison.Ordinal)
            || name.Contains("Remove", StringComparison.Ordinal)
            || name.Contains("Edit", StringComparison.Ordinal)
            || name.Contains("Update", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ASecondSubmitWhileBusy_IsIgnored()
    {
        var gate = new TaskCompletionSource<ApiCallResult<CreateEmployeeResponseDto>>();
        var api = new BlockingApi(gate.Task);
        var form = Filled(api);

        var first = form.SubmitAsync();
        await form.SubmitAsync();
        gate.SetResult(Created());
        await first;

        Assert.Equal(1, api.Calls);
    }

    private sealed class BlockingApi(Task<ApiCallResult<CreateEmployeeResponseDto>> pending) : IEmployeesApi
    {
        public int Calls { get; private set; }

        public Task<ApiCallResult<CreateEmployeeResponseDto>> CreateAsync(CreateEmployeeRequestDto request, CancellationToken cancellationToken = default)
        {
            Calls++;
            return pending;
        }

        public Task<ApiCallResult<TemporaryPasswordResponseDto>> RegenerateTemporaryPasswordAsync(Guid memberId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
