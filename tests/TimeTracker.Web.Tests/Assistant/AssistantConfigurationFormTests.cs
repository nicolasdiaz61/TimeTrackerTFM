using System.Net;

using TimeTracker.Web.Assistant;
using TimeTracker.Web.Http;
using TimeTracker.Web.Tests.Support;

namespace TimeTracker.Web.Tests.Assistant;

public class AssistantConfigurationFormTests
{
    private const string TypedKey = "clave-prueba-123456";

    private static readonly IReadOnlyDictionary<string, string[]> NoFieldErrors = new Dictionary<string, string[]>();

    private static readonly AllowedProviderDto[] Allowed =
    [
        new("OpenAI", true, true),
        new("Ollama", false, false),
    ];

    private static AssistantConfigurationDto Dto(
        string provider = "OpenAI",
        string? model = "modelo-ejemplo",
        string? effort = "medium",
        string source = "env",
        bool credentialConfigured = false,
        bool storageAvailable = true,
        string token = "7",
        IReadOnlyList<AllowedProviderDto>? allowed = null)
        => new(
            provider, model, 30, 1024, effort,
            new AssistantConfigurationSourcesDto(source, source, source, source, source, credentialConfigured ? "stored" : "none"),
            credentialConfigured, storageAvailable, allowed ?? Allowed, token, null, null, true);

    private static AssistantConfigurationForm Loaded(AssistantConfigurationDto? dto = null)
    {
        var form = new AssistantConfigurationForm();
        form.CompleteLoad(dto ?? Dto());
        return form;
    }

    private static AssistantConfigurationError Error(HttpStatusCode? status, string message = "Mensaje.", IReadOnlyDictionary<string, string[]>? fields = null, int? retryAfter = null)
        => new(new ApiRequestException(message, status), fields ?? NoFieldErrors, retryAfter);

    [Fact]
    public void CompleteLoad_CopiesTheEffectiveValuesAndLeavesTheCredentialEmpty()
    {
        var form = Loaded(Dto(credentialConfigured: true, source: "stored"));

        Assert.True(form.IsLoaded);
        Assert.Equal("OpenAI", form.Provider);
        Assert.Equal("modelo-ejemplo", form.Model);
        Assert.Equal(30, form.TimeoutSeconds);
        Assert.Equal(1024, form.MaxOutputTokens);
        Assert.Equal("medium", form.Effort);
        Assert.Null(form.Credential);
        Assert.True(form.CredentialConfigured);
    }

    [Fact]
    public void ProviderOptions_AreTheAllowedProvidersPlusDisabled()
    {
        var form = Loaded();

        Assert.Equal(["OpenAI", "Ollama", "None"], form.ProviderOptions.Select(option => option.Value));
        Assert.Equal("Deshabilitado", form.ProviderOptions[^1].Label);
    }

    [Fact]
    public void ProviderOptions_DoNotDuplicateNoneWhenTheServerListsIt()
    {
        var form = Loaded(Dto(allowed: [new AllowedProviderDto("None", false, false), new AllowedProviderDto("Ollama", false, false)]));

        Assert.Single(form.ProviderOptions, option => option.Value == "None");
    }

    [Fact]
    public void ProviderOptions_KeepTheCurrentProviderSelectableEvenIfTheListNoLongerHasIt()
    {
        var form = Loaded(Dto(provider: "Gemini", allowed: [new AllowedProviderDto("Ollama", false, false)]));

        Assert.Contains(form.ProviderOptions, option => option.Value == "Gemini");
    }

    [Fact]
    public void ShowsEffortOnlyWhenTheSelectedProviderSupportsIt()
    {
        var form = Loaded();
        Assert.True(form.EffortVisible);

        form.Provider = "Ollama";
        Assert.False(form.EffortVisible);

        form.Provider = "None";
        Assert.False(form.EffortVisible);

        form.Provider = "OpenAI";
        Assert.True(form.EffortVisible);
    }

    [Fact]
    public void BuildSaveRequest_UnchangedValuesFromTheEnvironmentAreOmitted()
    {
        var form = Loaded(Dto(source: "env"));

        var request = form.BuildSaveRequest();

        Assert.Equal("OpenAI", request.Provider);
        Assert.Null(request.Model);
        Assert.Null(request.TimeoutSeconds);
        Assert.Null(request.MaxOutputTokens);
        Assert.Null(request.Effort);
        Assert.Null(request.Credential);
        Assert.Equal("7", request.ConcurrencyToken);
    }

    [Fact]
    public void BuildSaveRequest_StoredValuesAreKeptAndChangedEnvironmentValuesAreSent()
    {
        var form = Loaded(Dto(source: "stored"));
        Assert.Equal("modelo-ejemplo", form.BuildSaveRequest().Model);
        Assert.Equal(30, form.BuildSaveRequest().TimeoutSeconds);

        var fromEnv = Loaded(Dto(source: "env"));
        fromEnv.Model = "otro-modelo";
        fromEnv.TimeoutSeconds = 45;
        var request = fromEnv.BuildSaveRequest();
        Assert.Equal("otro-modelo", request.Model);
        Assert.Equal(45, request.TimeoutSeconds);
        Assert.Null(request.MaxOutputTokens);
    }

    [Fact]
    public void BuildSaveRequest_ClearedFieldsGoAsNull()
    {
        var form = Loaded(Dto(source: "stored"));
        form.Model = "   ";
        form.TimeoutSeconds = null;
        form.Effort = "";

        var request = form.BuildSaveRequest();

        Assert.Null(request.Model);
        Assert.Null(request.TimeoutSeconds);
        Assert.Null(request.Effort);
    }

    [Fact]
    public void BuildSaveRequest_ChangingTheProviderSendsEveryValueExplicitly()
    {
        var form = Loaded(Dto(source: "env"));
        form.Provider = "Ollama";

        var request = form.BuildSaveRequest();

        Assert.Equal("Ollama", request.Provider);
        Assert.Equal("modelo-ejemplo", request.Model);
        Assert.Equal(30, request.TimeoutSeconds);
        Assert.Equal(1024, request.MaxOutputTokens);
    }

    [Fact]
    public void BuildSaveRequest_OmitsEffortWhenTheProviderDoesNotSupportIt()
    {
        var form = Loaded(Dto(source: "stored"));
        form.Provider = "Ollama";

        Assert.Null(form.BuildSaveRequest().Effort);
    }

    [Fact]
    public void BuildSaveRequest_SendsTheCredentialOnlyWhenSomethingWasTyped()
    {
        var form = Loaded(Dto(credentialConfigured: true));
        Assert.Null(form.BuildSaveRequest().Credential);

        form.Credential = "   ";
        Assert.Null(form.BuildSaveRequest().Credential);

        form.Credential = TypedKey;
        Assert.Equal(TypedKey, form.BuildSaveRequest().Credential);
    }

    [Fact]
    public void WithoutCredentialStorage_TheFieldIsDisabledAndNeverSent()
    {
        var form = Loaded(Dto(storageAvailable: false));
        form.Credential = TypedKey;

        Assert.False(form.CredentialEnabled);
        Assert.Null(form.BuildSaveRequest().Credential);
        Assert.Null(form.BuildTestRequest().Credential);
    }

    [Fact]
    public void BuildTestRequest_UsesTheSameMappingWithoutTheToken()
    {
        var form = Loaded(Dto(source: "env"));
        form.Model = "otro-modelo";
        form.Credential = TypedKey;

        var request = form.BuildTestRequest();

        Assert.Equal("OpenAI", request.Provider);
        Assert.Equal("otro-modelo", request.Model);
        Assert.Null(request.TimeoutSeconds);
        Assert.Equal(TypedKey, request.Credential);
    }

    [Fact]
    public void RequestsNeverPrintTheCredential()
    {
        var form = Loaded();
        form.Credential = TypedKey;

        Assert.DoesNotContain(TypedKey, form.BuildSaveRequest().ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(TypedKey, form.BuildTestRequest().ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void CompleteSave_ClearsTheCredentialAndShowsOnlyConfigured()
    {
        var form = Loaded();
        form.Credential = TypedKey;
        Assert.True(form.TryBegin());

        form.CompleteSave(Dto(credentialConfigured: true, token: "8"));

        Assert.Null(form.Credential);
        Assert.True(form.CredentialConfigured);
        Assert.False(form.IsBusy);
        Assert.Equal(AssistantConfigurationForm.SavedMessage, form.SuccessMessage);
        Assert.Equal("8", form.BuildSaveRequest().ConcurrencyToken);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.RequestEntityTooLarge)]
    [InlineData(null)]
    public void FailedSave_KeepsTheTypedCredentialAndTheOtherEdits_SoARetryDoesNotRetypeIt(HttpStatusCode? status)
    {
        var form = Loaded();
        form.Model = "otro-modelo";
        form.Credential = TypedKey;
        Assert.True(form.TryBegin());

        form.Fail(ConfigurationOperation.Save, Error(status));

        Assert.Equal(TypedKey, form.Credential);
        Assert.Equal("otro-modelo", form.Model);
        Assert.False(form.IsBusy);
    }

    [Fact]
    public void ReloadAfterConflict_ClearsTheTypedCredentialBecauseTheFormIsRebuiltFromTheServer()
    {
        var form = Loaded();
        form.Credential = TypedKey;
        Assert.True(form.TryBegin());
        form.Fail(ConfigurationOperation.Save, Error(HttpStatusCode.Conflict, "La configuración cambió."));

        Assert.True(form.TryBegin());
        form.CompleteLoad(Dto(token: "9"));

        Assert.Null(form.Credential);
    }

    [Fact]
    public void AResetWhoseRereadFails_SaysTheResetWasAppliedMarksTheLoadStaleAndOffersReloading()
    {
        var form = Loaded(Dto(credentialConfigured: true, source: "stored"));
        form.Credential = TypedKey;
        form.RequestReset();
        Assert.True(form.TryConfirmReset());

        form.Fail(ConfigurationOperation.ReloadAfterReset, Error(null, ApiRequest.GenericErrorMessage));

        Assert.False(form.IsLoaded);
        Assert.Null(form.Credential);
        Assert.Contains("restablecimiento sí se aplicó", form.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains(ApiRequest.GenericErrorMessage, form.ErrorMessage, StringComparison.Ordinal);
        Assert.True(form.ReloadOffered);
        Assert.False(form.IsBusy);
        Assert.Null(form.SuccessMessage);
        Assert.Throws<InvalidOperationException>(form.BuildSaveRequest);

        Assert.True(form.TryBegin());
        form.CompleteLoad(Dto(token: "0"));
        Assert.True(form.IsLoaded);
        Assert.Null(form.ErrorMessage);
    }

    [Fact]
    public void Conflict_ShowsTheServerMessageAndOffersReloading()
    {
        var form = Loaded();
        Assert.True(form.TryBegin());

        form.Fail(ConfigurationOperation.Save, Error(HttpStatusCode.Conflict, "La configuración cambió."));

        Assert.Equal("La configuración cambió.", form.ErrorMessage);
        Assert.True(form.ReloadOffered);

        Assert.True(form.TryBegin());
        form.CompleteLoad(Dto(token: "9"));
        Assert.False(form.ReloadOffered);
        Assert.Null(form.ErrorMessage);
    }

    [Fact]
    public void BadRequest_ShowsTheMessagesPerFieldAndGroupsTheUnknownOnes()
    {
        var form = Loaded();
        Assert.True(form.TryBegin());
        var fields = new Dictionary<string, string[]>
        {
            ["model"] = ["El campo model es obligatorio."],
            ["timeoutSeconds"] = ["Debe estar entre 5 y 55.", "Otro motivo."],
            ["concurrencyToken"] = ["El campo concurrencyToken es obligatorio."],
        };

        form.Fail(ConfigurationOperation.Save, Error(HttpStatusCode.BadRequest, fields: fields));

        Assert.Equal(["El campo model es obligatorio."], form.ErrorsFor("model"));
        Assert.Equal(2, form.ErrorsFor("timeoutSeconds").Count);
        Assert.Empty(form.ErrorsFor("effort"));
        Assert.Equal(["El campo concurrencyToken es obligatorio."], form.OtherErrors);
        Assert.Equal(AssistantConfigurationForm.ReviewFieldsMessage, form.ErrorMessage);
        Assert.False(form.ReloadOffered);
    }

    [Fact]
    public void BadRequestWithoutFields_ShowsTheServerMessage()
    {
        var form = Loaded();
        Assert.True(form.TryBegin());

        form.Fail(ConfigurationOperation.Save, Error(HttpStatusCode.BadRequest, "El cuerpo de la solicitud es obligatorio."));

        Assert.Equal("El cuerpo de la solicitud es obligatorio.", form.ErrorMessage);
    }

    [Fact]
    public void FieldErrorsAreClearedByTheNextOperation()
    {
        var form = Loaded();
        Assert.True(form.TryBegin());
        form.Fail(ConfigurationOperation.Save, Error(HttpStatusCode.BadRequest, fields: new Dictionary<string, string[]> { ["model"] = ["Mal."] }));

        Assert.True(form.TryBegin());

        Assert.Empty(form.ErrorsFor("model"));
        Assert.Null(form.ErrorMessage);
    }

    [Fact]
    public void ServerTextIsKeptLiteralAsPlainText()
    {
        var form = Loaded(Dto(model: "<b>x</b>", source: "stored"));
        Assert.Equal("<b>x</b>", form.Model);
        Assert.Equal("<b>x</b>", form.BuildSaveRequest().Model);

        Assert.True(form.TryBegin());
        form.Fail(ConfigurationOperation.Save, Error(HttpStatusCode.BadRequest, fields: new Dictionary<string, string[]> { ["model"] = ["<b>x</b>"] }));
        Assert.Equal("<b>x</b>", form.ErrorsFor("model")[0]);

        Assert.True(form.TryBegin());
        form.Fail(ConfigurationOperation.Save, Error(HttpStatusCode.Conflict, "<b>x</b>"));
        Assert.Equal("<b>x</b>", form.ErrorMessage);

        Assert.True(form.TryBegin());
        form.CompleteTest(new AssistantConnectionTestResponseDto("rejected", "<b>x</b>"));
        Assert.Equal("<b>x</b>", form.TestMessage);
    }

    [Theory]
    [InlineData(30, "Espere unos 30 segundos antes de reintentar.")]
    [InlineData(1, "Espere 1 segundo antes de reintentar.")]
    [InlineData(null, "")]
    public void TooManyRequestsOnTheTest_ShowsTheMessageWithTheWait(int? seconds, string hint)
    {
        var form = Loaded();
        Assert.True(form.TryBegin());

        form.Fail(ConfigurationOperation.Test, Error(HttpStatusCode.TooManyRequests, "Demasiadas pruebas.", retryAfter: seconds));

        Assert.Equal(("Demasiadas pruebas. " + hint).TrimEnd(), form.ErrorMessage);
    }

    [Fact]
    public void FailureWithoutStatus_ShowsTheGenericMessage()
    {
        var form = Loaded();
        Assert.True(form.TryBegin());

        form.Fail(ConfigurationOperation.Save, Error(null, ApiRequest.GenericErrorMessage));

        Assert.Equal(ApiRequest.GenericErrorMessage, form.ErrorMessage);
    }

    [Fact]
    public void FailedLoad_LeavesTheFormUnloaded()
    {
        var form = new AssistantConfigurationForm();
        Assert.True(form.TryBegin());

        form.Fail(ConfigurationOperation.Load, Error(null, ApiRequest.GenericErrorMessage));

        Assert.False(form.IsLoaded);
        Assert.Equal(ApiRequest.GenericErrorMessage, form.ErrorMessage);
    }

    [Fact]
    public void CompleteTest_KeepsTheTypedCredentialSoTheManagerCanSaveAfterwards()
    {
        var form = Loaded();
        form.Credential = TypedKey;
        Assert.True(form.TryBegin());

        form.CompleteTest(new AssistantConnectionTestResponseDto("ok", "La conexión funciona."));

        Assert.Equal(TypedKey, form.Credential);
        Assert.True(form.TestSucceeded);
        Assert.Equal("La conexión funciona.", form.TestMessage);
    }

    [Fact]
    public void CompleteTest_AnyOtherCategoryIsNotASuccessAndTheNextOperationClearsIt()
    {
        var form = Loaded();
        Assert.True(form.TryBegin());
        form.CompleteTest(new AssistantConnectionTestResponseDto("authentication", "La clave no es válida."));
        Assert.False(form.TestSucceeded);
        Assert.Equal("La clave no es válida.", form.TestMessage);

        Assert.True(form.TryBegin());

        Assert.Null(form.TestMessage);
    }

    [Fact]
    public void TryBegin_RefusesASecondOperationWhileBusy()
    {
        var form = Loaded();

        Assert.True(form.TryBegin());
        Assert.False(form.TryBegin());
    }

    [Fact]
    public void Reset_NeedsConfirmationAndCanBeCancelled()
    {
        var form = Loaded();
        Assert.False(form.ResetConfirmationPending);

        form.RequestReset();
        Assert.True(form.ResetConfirmationPending);

        form.CancelReset();
        Assert.False(form.ResetConfirmationPending);
    }

    [Fact]
    public void ConfirmReset_StartsTheOperationOnlyWhenConfirmationWasRequested()
    {
        var form = Loaded();
        Assert.False(form.TryConfirmReset());

        form.RequestReset();
        form.Credential = TypedKey;
        Assert.True(form.TryConfirmReset());
        Assert.False(form.ResetConfirmationPending);
        Assert.True(form.IsBusy);

        form.CompleteReset(Dto(token: "0"));

        Assert.Null(form.Credential);
        Assert.Equal(AssistantConfigurationForm.ResetMessage, form.SuccessMessage);
        Assert.Equal("0", form.BuildSaveRequest().ConcurrencyToken);
    }

    [Fact]
    public void FailedReset_KeepsTheTypedCredentialAndClearsTheConfirmation()
    {
        var form = Loaded();
        form.RequestReset();
        form.Credential = TypedKey;
        Assert.True(form.TryConfirmReset());

        form.Fail(ConfigurationOperation.Reset, Error(HttpStatusCode.Conflict, "La configuración cambió."));

        Assert.Equal(TypedKey, form.Credential);
        Assert.False(form.ResetConfirmationPending);
        Assert.True(form.ReloadOffered);
    }

    [Fact]
    public void WhileAnOperationRuns_TheResetCannotBeConfirmedAndTheConfirmationIsDropped()
    {
        var form = Loaded();
        form.RequestReset();
        Assert.True(form.CanConfirmReset);

        Assert.True(form.TryBegin());

        Assert.False(form.ResetConfirmationPending);
        Assert.False(form.CanConfirmReset);
        Assert.False(form.TryConfirmReset());
        Assert.True(form.IsBusy);
    }

    [Fact]
    public void ARequestToReset_IsIgnoredWhileAnOperationRuns()
    {
        var form = Loaded();
        Assert.True(form.TryBegin());

        form.RequestReset();

        Assert.False(form.CanConfirmReset);
    }

    [Theory]
    [InlineData("env", "Entorno")]
    [InlineData("stored", "Guardado por un responsable")]
    [InlineData("none", "Sin clave")]
    [InlineData("default", "Valor por defecto")]
    [InlineData("<b>x</b>", "<b>x</b>")]
    public void SourceLabel_TranslatesTheKnownOriginsAndKeepsTheRestAsText(string source, string label)
        => Assert.Equal(label, AssistantConfigurationForm.SourceLabel(source));

    [Fact]
    public void UpdatedText_ShowsTheLocalTimeAndWhoChangedIt()
    {
        var form = Loaded(Dto() with { UpdatedAt = new DateTimeOffset(2026, 10, 19, 8, 30, 0, TimeSpan.Zero), UpdatedBy = "<b>Ana</b>" });

        Assert.Equal("Última modificación: 19/10/2026 10:30 por <b>Ana</b>.", form.UpdatedText(new FixedOffsetTimeZone(TimeSpan.FromHours(2))));
    }

    [Fact]
    public void UpdatedText_WithoutAnAuthorOmitsIt_AndWithoutRowSaysTheEnvironmentApplies()
    {
        var withoutAuthor = Loaded(Dto() with { UpdatedAt = new DateTimeOffset(2026, 10, 19, 8, 30, 0, TimeSpan.Zero) });
        Assert.Equal("Última modificación: 19/10/2026 08:30.", withoutAuthor.UpdatedText(FixedOffsetTimeZone.Utc));

        Assert.Equal(AssistantConfigurationForm.NothingStoredMessage, Loaded().UpdatedText(FixedOffsetTimeZone.Utc));
    }
}
