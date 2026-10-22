using System.Net;

using TimeTracker.Web.Account;
using TimeTracker.Web.Auth;
using TimeTracker.Web.Http;
using TimeTracker.Web.Tests.Support;

namespace TimeTracker.Web.Tests.Account;

public class ChangePasswordFormTests
{
    private const string Current = "contrasena-temporal-1";
    private const string New = "contrasena-nueva-12345";

    private sealed class FakeApi(Func<ChangePasswordRequestDto, ApiCallResult<LoginResponseDto>> respond) : IPasswordChangeApi
    {
        public List<ChangePasswordRequestDto> Requests { get; } = [];

        public Task<ApiCallResult<LoginResponseDto>> ChangePasswordAsync(ChangePasswordRequestDto request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    private static ApiCallResult<LoginResponseDto> Changed()
        => new(new LoginResponseDto("token-nuevo", DateTime.UtcNow.AddMinutes(30)), null);

    private static ApiCallResult<LoginResponseDto> Failed(HttpStatusCode? status, string message = "Mensaje.", IReadOnlyDictionary<string, string[]>? fields = null, int? retryAfter = null)
        => new(null, Failures.Of(status, message, fields, retryAfter));

    private static ChangePasswordForm Filled(IPasswordChangeApi api, FakeSession session, string? returnUrl = null)
        => new(api, session) { CurrentPassword = Current, NewPassword = New, ConfirmPassword = New, ReturnUrlParameter = returnUrl };

    [Fact]
    public async Task Submit_SendsTheCurrentAndTheNewPassword_Untouched()
    {
        var api = new FakeApi(_ => Changed());
        var form = Filled(api, new FakeSession());
        form.CurrentPassword = " " + Current + " ";

        await form.SubmitAsync();

        Assert.Equal(new ChangePasswordRequestDto(" " + Current + " ", New), api.Requests.Single());
    }

    [Fact]
    public async Task Changed_ReplacesTheSessionWithTheNewToken_AndNavigatesHome_ClearingTheFields()
    {
        var session = new FakeSession();
        var form = Filled(new FakeApi(_ => Changed()), session);

        await form.SubmitAsync();

        Assert.Equal(["token-nuevo"], session.Tokens);
        Assert.Equal("/", form.NavigateTo);
        AssertCleared(form);
        Assert.Null(form.ErrorMessage);
    }

    [Fact]
    public async Task Changed_WithASafeReturnUrl_NavigatesThere()
    {
        var form = Filled(new FakeApi(_ => Changed()), new FakeSession(), "/equipo");

        await form.SubmitAsync();

        Assert.Equal("/equipo", form.NavigateTo);
    }

    [Theory]
    [InlineData("//ejemplo.test")]
    [InlineData("https://ejemplo.test")]
    public async Task Changed_WithAnUnsafeReturnUrl_NavigatesHome(string returnUrl)
    {
        var form = Filled(new FakeApi(_ => Changed()), new FakeSession(), returnUrl);

        await form.SubmitAsync();

        Assert.Equal("/", form.NavigateTo);
    }

    [Fact]
    public async Task BadRequest_ShowsTheCurrentPasswordErrorNextToItsField_AndClearsTheFields()
    {
        var fields = new Dictionary<string, string[]> { ["currentPassword"] = ["La contraseña actual no es correcta."] };
        var form = Filled(new FakeApi(_ => Failed(HttpStatusCode.BadRequest, fields: fields)), new FakeSession());

        await form.SubmitAsync();

        Assert.Equal(["La contraseña actual no es correcta."], form.ErrorsFor("currentPassword"));
        Assert.Empty(form.ErrorsFor("newPassword"));
        AssertCleared(form);
        Assert.Null(form.NavigateTo);
    }

    [Fact]
    public async Task TooManyRequests_AsksToWait_AndClearsTheFields()
    {
        var form = Filled(new FakeApi(_ => Failed(HttpStatusCode.TooManyRequests, retryAfter: 1)), new FakeSession());

        await form.SubmitAsync();

        Assert.StartsWith(ChangePasswordForm.TooManyRequestsMessage, form.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("1 segundo", form.ErrorMessage, StringComparison.Ordinal);
        AssertCleared(form);
    }

    [Fact]
    public async Task Conflict_ShowsTheApiDetail()
    {
        var form = Filled(new FakeApi(_ => Failed(HttpStatusCode.Conflict, "Su cuenta ha cambiado. Vuelva a intentarlo.")), new FakeSession());

        await form.SubmitAsync();

        Assert.Equal("Su cuenta ha cambiado. Vuelva a intentarlo.", form.ErrorMessage);
        AssertCleared(form);
    }

    [Fact]
    public async Task Forbidden_ShowsAPermissionMessage()
    {
        var form = Filled(new FakeApi(_ => Failed(HttpStatusCode.Forbidden)), new FakeSession());

        await form.SubmitAsync();

        Assert.Equal(ChangePasswordForm.ForbiddenMessage, form.ErrorMessage);
    }

    [Fact]
    public async Task Unauthorized_ShowsNoMessage_BecauseTheHandlerClosesTheSessionAndRedirects()
    {
        var form = Filled(new FakeApi(_ => Failed(HttpStatusCode.Unauthorized)), new FakeSession());

        await form.SubmitAsync();

        Assert.Null(form.ErrorMessage);
        Assert.Null(form.NavigateTo);
        AssertCleared(form);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(null)]
    public async Task OtherFailures_ShowTheGenericMessageWithoutTechnicalData(HttpStatusCode? status)
    {
        var form = Filled(new FakeApi(_ => Failed(status, "System.InvalidOperationException")), new FakeSession());

        await form.SubmitAsync();

        Assert.Equal(ApiRequest.GenericErrorMessage, form.ErrorMessage);
        AssertCleared(form);
    }

    [Fact]
    public async Task MismatchedConfirmation_DoesNotCallTheApi()
    {
        var api = new FakeApi(_ => Changed());
        var form = Filled(api, new FakeSession());
        form.ConfirmPassword = "otra-contrasena-distinta";

        await form.SubmitAsync();

        Assert.Empty(api.Requests);
        Assert.Equal([PasswordConfirmation.MismatchMessage], form.ErrorsFor("confirmPassword"));
    }

    [Fact]
    public void TheMandatoryNotice_ExplainsThatTheTemporaryPasswordMustBeChanged()
    {
        Assert.Contains("temporal", ChangePasswordForm.MandatoryChangeNotice, StringComparison.Ordinal);
        Assert.Contains("antes de continuar", ChangePasswordForm.MandatoryChangeNotice, StringComparison.Ordinal);
    }

    private static void AssertCleared(ChangePasswordForm form)
    {
        Assert.Equal(string.Empty, form.CurrentPassword);
        Assert.Equal(string.Empty, form.NewPassword);
        Assert.Equal(string.Empty, form.ConfirmPassword);
    }
}
