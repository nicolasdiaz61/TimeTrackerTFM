using System.Net;

using TimeTracker.Web.Auth;
using TimeTracker.Web.Http;
using TimeTracker.Web.Tests.Support;

namespace TimeTracker.Web.Tests.Account;

public class LoginFormTests
{
    private const string Password = "contrasena-prueba-12345";

    private sealed class FakeApi(Func<string, string, LoginResponseDto> respond) : ILoginApi
    {
        public List<(string Email, string Password)> Calls { get; } = [];

        public Task<LoginResponseDto> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
        {
            Calls.Add((email, password));
            return Task.FromResult(respond(email, password));
        }
    }

    private sealed class FailingApi(ApiRequestException exception) : ILoginApi
    {
        public Task<LoginResponseDto> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
            => throw exception;
    }

    private static LoginResponseDto Response(string token) => new(token, DateTime.UtcNow.AddMinutes(30));

    private static LoginForm FormFor(ILoginApi api, FakeSession session, string? returnUrl = null)
        => new(api, session) { Email = " luis@example.test ", Password = Password, ReturnUrlParameter = returnUrl };

    [Fact]
    public async Task Success_WithoutAPendingChange_NavigatesHome_AndSendsTheTrimmedEmail()
    {
        var api = new FakeApi((_, _) => Response(Tokens.WithoutPendingChange()));
        var session = new FakeSession();
        var form = FormFor(api, session);

        await form.SubmitAsync();

        Assert.Equal("/", form.NavigateTo);
        Assert.Equal([("luis@example.test", Password)], api.Calls);
        Assert.Single(session.Tokens);
        Assert.Equal(string.Empty, form.Password);
        Assert.Null(form.ErrorMessage);
    }

    [Fact]
    public async Task Success_WithASafeReturnUrl_NavigatesThere()
    {
        var form = FormFor(new FakeApi((_, _) => Response(Tokens.WithoutPendingChange())), new FakeSession(), "/equipo");

        await form.SubmitAsync();

        Assert.Equal("/equipo", form.NavigateTo);
    }

    [Theory]
    [InlineData("//ejemplo.test")]
    [InlineData("https://ejemplo.test")]
    [InlineData("/\\ejemplo.test")]
    [InlineData("equipo")]
    public async Task Success_WithAnUnsafeReturnUrl_IgnoresItAndNavigatesHome(string returnUrl)
    {
        var form = FormFor(new FakeApi((_, _) => Response(Tokens.WithoutPendingChange())), new FakeSession(), returnUrl);

        await form.SubmitAsync();

        Assert.Equal("/", form.NavigateTo);
    }

    [Fact]
    public async Task Success_WithAPendingChange_NavigatesToTheChangePage_NotHome()
    {
        var form = FormFor(new FakeApi((_, _) => Response(Tokens.WithPendingChange())), new FakeSession());

        await form.SubmitAsync();

        Assert.Equal("/cambiar-contrasena", form.NavigateTo);
    }

    [Fact]
    public async Task Success_WithAPendingChangeAndASafeReturnUrl_KeepsTheReturnUrl()
    {
        var form = FormFor(new FakeApi((_, _) => Response(Tokens.WithPendingChange())), new FakeSession(), "/equipo");

        await form.SubmitAsync();

        Assert.Equal("/cambiar-contrasena?returnUrl=%2Fequipo", form.NavigateTo);
    }

    [Theory]
    [InlineData("//ejemplo.test")]
    [InlineData("https://ejemplo.test")]
    public async Task Success_WithAPendingChangeAndAnUnsafeReturnUrl_DropsTheReturnUrl(string returnUrl)
    {
        var form = FormFor(new FakeApi((_, _) => Response(Tokens.WithPendingChange())), new FakeSession(), returnUrl);

        await form.SubmitAsync();

        Assert.Equal("/cambiar-contrasena", form.NavigateTo);
    }

    [Fact]
    public async Task Unauthorized_ShowsTheSameGenericMessageForAWrongPasswordAndAnUnknownEmail()
    {
        var wrongPassword = FormFor(new FailingApi(new ApiRequestException("Correo o contraseña (detalle de la API).", HttpStatusCode.Unauthorized)), new FakeSession());
        var unknownEmail = FormFor(new FailingApi(new ApiRequestException("Otro detalle.", HttpStatusCode.Unauthorized)), new FakeSession());

        await wrongPassword.SubmitAsync();
        await unknownEmail.SubmitAsync();

        Assert.Equal(LoginForm.InvalidCredentialsMessage, wrongPassword.ErrorMessage);
        Assert.Equal(wrongPassword.ErrorMessage, unknownEmail.ErrorMessage);
        Assert.Equal(string.Empty, wrongPassword.Password);
        Assert.Null(wrongPassword.NavigateTo);
    }

    [Fact]
    public async Task TooManyRequests_AsksToWait_AndClearsThePassword()
    {
        var form = FormFor(new FailingApi(new ApiRequestException("x", HttpStatusCode.TooManyRequests)), new FakeSession());

        await form.SubmitAsync();

        Assert.Equal(LoginForm.TooManyRequestsMessage, form.ErrorMessage);
        Assert.Equal(string.Empty, form.Password);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(null)]
    public async Task OtherFailures_ShowTheGenericMessageWithoutTechnicalData(HttpStatusCode? status)
    {
        var form = FormFor(new FailingApi(new ApiRequestException(ApiRequest.GenericErrorMessage, status)), new FakeSession());

        await form.SubmitAsync();

        Assert.Equal(ApiRequest.GenericErrorMessage, form.ErrorMessage);
        Assert.Equal(string.Empty, form.Password);
    }

    [Theory]
    [InlineData("", Password)]
    [InlineData("   ", Password)]
    [InlineData("luis@example.test", "")]
    public async Task EmptyFields_DoNotSendTheRequest(string email, string password)
    {
        var api = new FakeApi((_, _) => Response(Tokens.WithoutPendingChange()));
        var form = new LoginForm(api, new FakeSession()) { Email = email, Password = password };

        await form.SubmitAsync();

        Assert.Empty(api.Calls);
        Assert.Equal(LoginForm.EmptyFieldsMessage, form.ErrorMessage);
        Assert.Null(form.NavigateTo);
    }

    [Fact]
    public async Task APasswordWithOuterSpaces_IsSentUntouched()
    {
        var api = new FakeApi((_, _) => Response(Tokens.WithoutPendingChange()));
        var form = FormFor(api, new FakeSession());
        form.Password = "  con espacios  ";

        await form.SubmitAsync();

        Assert.Equal("  con espacios  ", api.Calls.Single().Password);
    }
}
