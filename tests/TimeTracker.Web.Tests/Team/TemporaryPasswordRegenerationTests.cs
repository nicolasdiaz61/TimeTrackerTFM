using System.Net;

using TimeTracker.Web.Http;
using TimeTracker.Web.Team;
using TimeTracker.Web.Tests.Support;

namespace TimeTracker.Web.Tests.Team;

public class TemporaryPasswordRegenerationTests
{
    private const string Temporary = "NuevaTemporal1357Zq";

    private static readonly Guid Ana = Guid.Parse("a1111111-1111-1111-1111-111111111111");

    private sealed class FakeApi(Func<Guid, ApiCallResult<TemporaryPasswordResponseDto>> respond) : IEmployeesApi
    {
        public List<Guid> Regenerated { get; } = [];

        public Task<ApiCallResult<CreateEmployeeResponseDto>> CreateAsync(CreateEmployeeRequestDto request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("La regeneración nunca da de alta.");

        public Task<ApiCallResult<TemporaryPasswordResponseDto>> RegenerateTemporaryPasswordAsync(Guid memberId, CancellationToken cancellationToken = default)
        {
            Regenerated.Add(memberId);
            return Task.FromResult(respond(memberId));
        }
    }

    private static ApiCallResult<TemporaryPasswordResponseDto> Regenerated()
        => new(new TemporaryPasswordResponseDto(Temporary), null);

    private static ApiCallResult<TemporaryPasswordResponseDto> Failed(HttpStatusCode? status, string message = "Mensaje.", int? retryAfter = null)
        => new(null, Failures.Of(status, message, retryAfter: retryAfter));

    [Fact]
    public void RequestingARegeneration_OnlyAsksForConfirmation_WithoutCallingTheApi()
    {
        var api = new FakeApi(_ => Regenerated());
        var regeneration = new TeamMemberTemporaryPassword(api);

        regeneration.Request(Ana, "Ana Ejemplo");

        Assert.Empty(api.Regenerated);
        Assert.True(regeneration.ConfirmationPending);
        Assert.Equal("Ana Ejemplo", regeneration.PendingName);
    }

    [Fact]
    public async Task Confirming_CallsTheApiWithTheMemberId_AndShowsThePasswordOnceWithTheNotice()
    {
        var api = new FakeApi(_ => Regenerated());
        var regeneration = new TeamMemberTemporaryPassword(api);
        regeneration.Request(Ana, "Ana Ejemplo");

        await regeneration.ConfirmAsync();

        Assert.Equal([Ana], api.Regenerated);
        Assert.Equal(Temporary, regeneration.Reveal.Value);
        Assert.Equal("Ana Ejemplo", regeneration.Reveal.PersonName);
        Assert.False(regeneration.ConfirmationPending);
        Assert.Null(regeneration.ErrorMessage);
    }

    [Fact]
    public async Task Confirming_WithoutARequest_DoesNothing()
    {
        var api = new FakeApi(_ => Regenerated());
        var regeneration = new TeamMemberTemporaryPassword(api);

        await regeneration.ConfirmAsync();

        Assert.Empty(api.Regenerated);
    }

    [Fact]
    public async Task Cancelling_DropsThePendingConfirmation_SoConfirmingDoesNothing()
    {
        var api = new FakeApi(_ => Regenerated());
        var regeneration = new TeamMemberTemporaryPassword(api);
        regeneration.Request(Ana, "Ana Ejemplo");

        regeneration.Cancel();
        await regeneration.ConfirmAsync();

        Assert.Empty(api.Regenerated);
        Assert.False(regeneration.ConfirmationPending);
    }

    [Fact]
    public async Task Dismissing_RemovesThePasswordFromTheState()
    {
        var regeneration = new TeamMemberTemporaryPassword(new FakeApi(_ => Regenerated()));
        regeneration.Request(Ana, "Ana Ejemplo");
        await regeneration.ConfirmAsync();

        regeneration.Reveal.Dismiss();

        Assert.Null(regeneration.Reveal.Value);
        Assert.False(regeneration.Reveal.IsVisible);
    }

    [Fact]
    public async Task ANewRequest_RemovesAPasswordAlreadyShown()
    {
        var regeneration = new TeamMemberTemporaryPassword(new FakeApi(_ => Regenerated()));
        regeneration.Request(Ana, "Ana Ejemplo");
        await regeneration.ConfirmAsync();

        regeneration.Request(Guid.NewGuid(), "Otra Persona");

        Assert.Null(regeneration.Reveal.Value);
    }

    [Fact]
    public async Task Conflict_ShowsTheApiDetail_AndNoPassword()
    {
        var regeneration = new TeamMemberTemporaryPassword(new FakeApi(_ => Failed(HttpStatusCode.Conflict, "La persona ya cambió su contraseña.")));
        regeneration.Request(Ana, "Ana Ejemplo");

        await regeneration.ConfirmAsync();

        Assert.Equal("La persona ya cambió su contraseña.", regeneration.ErrorMessage);
        Assert.Null(regeneration.Reveal.Value);
        Assert.False(regeneration.IsBusy);
    }

    [Fact]
    public async Task NotFound_SaysThePersonIsNoLongerInTheTeam()
    {
        var regeneration = new TeamMemberTemporaryPassword(new FakeApi(_ => Failed(HttpStatusCode.NotFound)));
        regeneration.Request(Ana, "Ana Ejemplo");

        await regeneration.ConfirmAsync();

        Assert.Equal(TeamMemberTemporaryPassword.NotFoundMessage, regeneration.ErrorMessage);
        Assert.Null(regeneration.Reveal.Value);
    }

    [Fact]
    public async Task Forbidden_ShowsAPermissionMessage_AndUnauthorizedShowsNone()
    {
        var forbidden = new TeamMemberTemporaryPassword(new FakeApi(_ => Failed(HttpStatusCode.Forbidden)));
        forbidden.Request(Ana, "Ana Ejemplo");
        var unauthorized = new TeamMemberTemporaryPassword(new FakeApi(_ => Failed(HttpStatusCode.Unauthorized)));
        unauthorized.Request(Ana, "Ana Ejemplo");

        await forbidden.ConfirmAsync();
        await unauthorized.ConfirmAsync();

        Assert.Equal(TeamMemberTemporaryPassword.NoPermissionMessage, forbidden.ErrorMessage);
        Assert.Null(unauthorized.ErrorMessage);
    }

    [Fact]
    public async Task TooManyRequests_AsksToWait()
    {
        var regeneration = new TeamMemberTemporaryPassword(new FakeApi(_ => Failed(HttpStatusCode.TooManyRequests, retryAfter: 15)));
        regeneration.Request(Ana, "Ana Ejemplo");

        await regeneration.ConfirmAsync();

        Assert.StartsWith(TeamMemberTemporaryPassword.TooManyRequestsMessage, regeneration.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("15 segundos", regeneration.ErrorMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(null)]
    public async Task OtherFailures_ShowTheGenericMessage(HttpStatusCode? status)
    {
        var regeneration = new TeamMemberTemporaryPassword(new FakeApi(_ => Failed(status, "System.Exception")));
        regeneration.Request(Ana, "Ana Ejemplo");

        await regeneration.ConfirmAsync();

        Assert.Equal(ApiRequest.GenericErrorMessage, regeneration.ErrorMessage);
    }

    [Fact]
    public async Task ASecondConfirmWhileBusy_IsIgnored()
    {
        var gate = new TaskCompletionSource<ApiCallResult<TemporaryPasswordResponseDto>>();
        var calls = 0;
        var regeneration = new TeamMemberTemporaryPassword(new GatedApi(gate.Task, () => calls++));
        regeneration.Request(Ana, "Ana Ejemplo");

        var first = regeneration.ConfirmAsync();
        await regeneration.ConfirmAsync();
        gate.SetResult(Regenerated());
        await first;

        Assert.Equal(1, calls);
    }

    private sealed class GatedApi(Task<ApiCallResult<TemporaryPasswordResponseDto>> pending, Action onCall) : IEmployeesApi
    {
        public Task<ApiCallResult<CreateEmployeeResponseDto>> CreateAsync(CreateEmployeeRequestDto request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ApiCallResult<TemporaryPasswordResponseDto>> RegenerateTemporaryPasswordAsync(Guid memberId, CancellationToken cancellationToken = default)
        {
            onCall();
            return pending;
        }
    }
}
