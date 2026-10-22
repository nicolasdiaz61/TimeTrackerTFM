using System.Net;

using TimeTracker.Web.Auth;
using TimeTracker.Web.Http;
using TimeTracker.Web.Registration;
using TimeTracker.Web.Tests.Support;

namespace TimeTracker.Web.Tests.Registration;

public class RegisterFormTests
{
    private const string Password = "contrasena-prueba-12345";
    private const string Code = "codigo-prueba-0123456789";

    private sealed class FakeApi(Func<RegisterCompanyRequestDto, ApiCallResult<LoginResponseDto>> respond) : IRegistrationApi
    {
        public List<RegisterCompanyRequestDto> Requests { get; } = [];

        public Task<ApiCallResult<LoginResponseDto>> RegisterCompanyAsync(RegisterCompanyRequestDto request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    private sealed class BlockingApi(Task<ApiCallResult<LoginResponseDto>> pending) : IRegistrationApi
    {
        public int Calls { get; private set; }

        public Task<ApiCallResult<LoginResponseDto>> RegisterCompanyAsync(RegisterCompanyRequestDto request, CancellationToken cancellationToken = default)
        {
            Calls++;
            return pending;
        }
    }

    private static ApiCallResult<LoginResponseDto> Created()
        => new(new LoginResponseDto("token-ficticio", DateTime.UtcNow.AddMinutes(30)), null);

    private static ApiCallResult<LoginResponseDto> Failed(ApiFailure failure) => new(null, failure);

    private static RegisterForm Filled(IRegistrationApi api, FakeSession session)
        => new(api, session)
        {
            CompanyName = "  Compañía Ejemplo ",
            ManagerName = " Ana Ejemplo ",
            Email = " ana@example.test ",
            Password = Password,
            ConfirmPassword = Password,
            InviteCode = Code,
        };

    [Fact]
    public async Task Submit_SendsTheTrimmedFieldsAndTheUntouchedPasswordAndCode()
    {
        var api = new FakeApi(_ => Created());
        var form = Filled(api, new FakeSession());

        await form.SubmitAsync();

        var request = Assert.Single(api.Requests);
        Assert.Equal(new RegisterCompanyRequestDto("Compañía Ejemplo", "Ana Ejemplo", "ana@example.test", Password, Code), request);
    }

    [Fact]
    public async Task Submit_WithAPasswordWithOuterSpaces_DoesNotTrimIt()
    {
        var api = new FakeApi(_ => Created());
        var form = Filled(api, new FakeSession());
        form.Password = form.ConfirmPassword = "  con espacios  ";

        await form.SubmitAsync();

        Assert.Equal("  con espacios  ", api.Requests.Single().Password);
    }

    [Fact]
    public async Task Created_SignsInWithTheTokenNavigatesHomeAndClearsPasswordsAndCode()
    {
        var session = new FakeSession();
        var form = Filled(new FakeApi(_ => Created()), session);

        await form.SubmitAsync();

        Assert.Equal(["token-ficticio"], session.Tokens);
        Assert.Equal("/", form.NavigateTo);
        Assert.Null(form.ErrorMessage);
        AssertSecretsCleared(form);
        Assert.False(form.IsBusy);
    }

    [Fact]
    public async Task MismatchedConfirmation_DoesNotCallTheApi_AndMarksTheConfirmationField()
    {
        var api = new FakeApi(_ => Created());
        var form = Filled(api, new FakeSession());
        form.ConfirmPassword = "otra-contrasena-distinta";

        await form.SubmitAsync();

        Assert.Empty(api.Requests);
        Assert.Equal([RegisterForm.ConfirmationMismatchMessage], form.ErrorsFor("confirmPassword"));
        Assert.Null(form.NavigateTo);
    }

    [Fact]
    public async Task BadRequest_ShowsEachFieldErrorNextToItsFieldAndKeepsTheDataButNotTheSecrets()
    {
        var fields = new Dictionary<string, string[]>
        {
            ["password"] = ["La contraseña debe tener al menos 12 caracteres."],
            ["email"] = ["El correo electrónico debe ser válido."],
        };
        var form = Filled(new FakeApi(_ => Failed(Failures.Of(HttpStatusCode.BadRequest, fields: fields))), new FakeSession());

        await form.SubmitAsync();

        Assert.Equal(["La contraseña debe tener al menos 12 caracteres."], form.ErrorsFor("password"));
        Assert.Equal(["El correo electrónico debe ser válido."], form.ErrorsFor("email"));
        Assert.Empty(form.ErrorsFor("companyName"));
        Assert.Equal(RegisterForm.ReviewFieldsMessage, form.ErrorMessage);
        Assert.Equal("  Compañía Ejemplo ", form.CompanyName);
        AssertSecretsCleared(form);
    }

    [Fact]
    public async Task Forbidden_ShowsAGenericInviteCodeMessage_AndClearsTheSecrets()
    {
        var form = Filled(new FakeApi(_ => Failed(Failures.Of(HttpStatusCode.Forbidden, "El código no coincide con ninguno de la lista."))), new FakeSession());

        await form.SubmitAsync();

        Assert.Equal(RegisterForm.InvalidInviteCodeMessage, form.ErrorMessage);
        Assert.DoesNotContain("lista", form.ErrorMessage, StringComparison.Ordinal);
        AssertSecretsCleared(form);
        Assert.Null(form.NavigateTo);
    }

    [Fact]
    public async Task Conflict_ShowsTheApiDetail_AndKeepsCompanyNameAndEmail()
    {
        var form = Filled(new FakeApi(_ => Failed(Failures.Of(HttpStatusCode.Conflict, "Ya existe una compañía con ese nombre."))), new FakeSession());

        await form.SubmitAsync();

        Assert.Equal("Ya existe una compañía con ese nombre.", form.ErrorMessage);
        Assert.Equal("  Compañía Ejemplo ", form.CompanyName);
        Assert.Equal(" Ana Ejemplo ", form.ManagerName);
        Assert.Equal(" ana@example.test ", form.Email);
        AssertSecretsCleared(form);
    }

    [Fact]
    public async Task TooManyRequests_AsksToWaitWithTheHint()
    {
        var form = Filled(new FakeApi(_ => Failed(Failures.Of(HttpStatusCode.TooManyRequests, retryAfter: 90))), new FakeSession());

        await form.SubmitAsync();

        Assert.StartsWith(RegisterForm.TooManyRequestsMessage, form.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("90 segundos", form.ErrorMessage, StringComparison.Ordinal);
        AssertSecretsCleared(form);
        Assert.False(form.IsBusy);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task ClosedRegistration_ShowsTheUnavailableMessage_WithoutSignOutOrNavigationToLogin(HttpStatusCode status)
    {
        var session = new FakeSession();
        var form = Filled(new FakeApi(_ => Failed(Failures.Of(status))), session);

        await form.SubmitAsync();

        Assert.Equal("El registro no está disponible.", form.ErrorMessage);
        Assert.Null(form.NavigateTo);
        Assert.Empty(session.Tokens);
        AssertSecretsCleared(form);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(null)]
    public async Task OtherFailures_ShowTheGenericMessageWithoutTechnicalData(HttpStatusCode? status)
    {
        var form = Filled(new FakeApi(_ => Failed(Failures.Of(status, "System.NullReferenceException en Foo.Bar"))), new FakeSession());

        await form.SubmitAsync();

        Assert.Equal(ApiRequest.GenericErrorMessage, form.ErrorMessage);
        AssertSecretsCleared(form);
    }

    [Fact]
    public async Task AFailureAfterAnotherClearsThePreviousFieldErrors()
    {
        var calls = 0;
        var form = Filled(
            new FakeApi(_ => ++calls == 1
                ? Failed(Failures.Of(HttpStatusCode.BadRequest, fields: new Dictionary<string, string[]> { ["email"] = ["Correo no válido."] }))
                : Failed(Failures.Of(HttpStatusCode.Conflict, "Duplicado."))),
            new FakeSession());

        await form.SubmitAsync();
        form.Password = form.ConfirmPassword = Password;
        await form.SubmitAsync();

        Assert.Empty(form.ErrorsFor("email"));
        Assert.Equal("Duplicado.", form.ErrorMessage);
    }

    [Fact]
    public async Task ASecondSubmitWhileBusy_IsIgnored()
    {
        var gate = new TaskCompletionSource<ApiCallResult<LoginResponseDto>>();
        var api = new BlockingApi(gate.Task);
        var form = Filled(api, new FakeSession());

        var first = form.SubmitAsync();
        await form.SubmitAsync();
        gate.SetResult(Created());
        await first;

        Assert.Equal(1, api.Calls);
    }

    private static void AssertSecretsCleared(RegisterForm form)
    {
        Assert.Equal(string.Empty, form.Password);
        Assert.Equal(string.Empty, form.ConfirmPassword);
        Assert.Equal(string.Empty, form.InviteCode);
    }
}
