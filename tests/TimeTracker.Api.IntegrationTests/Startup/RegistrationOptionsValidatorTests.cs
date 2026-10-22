using System.Net;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using TimeTracker.Api.Registration;

namespace TimeTracker.Api.IntegrationTests.Startup;

/// <summary>
/// Opciones del registro de compañías (startup-configuration, «Registro de compañías configurado y validado»). Los validadores viven
/// en <c>TimeTracker.Api</c>, por eso se prueban aquí. Los códigos son valores inventados y reconocibles; ninguno debe imprimirse.
/// </summary>
public class RegistrationOptionsValidatorTests
{
    private const string EnabledKey = "Registration:Enabled";
    private const string CodesKey = "Registration:InviteCodes";
    private const string ValidCode = "codigo-prueba-0123456789";

    private static Exception? StartWith(params (string Key, string? Value)[] settings)
    {
        using var factory = CreateFactory(settings);
        return factory.StartExpectingFailure();
    }

    private static async Task AssertStartsAsync(params (string Key, string? Value)[] settings)
    {
        using var factory = CreateFactory(settings);

        var response = await factory.CreateAnonymousClient().GetAsync("/health", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> CreateFactory((string Key, string? Value)[] settings)
        => new ApiFactory().WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }
        });

    [Fact]
    public async Task WithoutSettings_RegistrationIsClosed_NoCodesAreNeeded_AndTheApiStarts()
    {
        using var factory = new ApiFactory();

        var response = await factory.CreateAnonymousClient().GetAsync("/health", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var options = factory.Services.GetRequiredService<IOptions<RegistrationOptions>>().Value;
        Assert.False(options.Enabled);
        Assert.Empty(options.ParseInviteCodes());
    }

    [Fact]
    public void ANonBooleanFlag_FailsNamingTheKey()
    {
        var exception = StartWith((EnabledKey, "quizas"));

        Assert.NotNull(exception);
        Assert.Contains(EnabledKey, StartupFailure.AllMessages(exception), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AnOpenRegistrationWithoutCodes_FailsNamingTheCodesKey(string? codes)
    {
        var exception = StartWith((EnabledKey, "true"), (CodesKey, codes));

        DatabaseStartupTests.AssertStartupFailsNamingKey(exception, CodesKey);
    }

    [Theory]
    [InlineData("corto-123")]
    [InlineData("codigo-largo-0123456789,")]
    [InlineData("codigo-largo-0123456789, ,otro-codigo-largo-0123456789")]
    [InlineData("codigo-largo-0123456789,corto")]
    public void AnOpenRegistrationWithAShortCodeOrABlankElement_FailsNamingTheCodesKey(string codes)
    {
        var exception = StartWith((EnabledKey, "true"), (CodesKey, codes));

        DatabaseStartupTests.AssertStartupFailsNamingKey(exception, CodesKey);
        Assert.DoesNotContain("codigo-largo-0123456789", StartupFailure.AllMessages(exception), StringComparison.Ordinal);
    }

    [Fact]
    public void AnOpenRegistrationWithACodeOver128CharactersAfterTrimming_Fails()
    {
        var exception = StartWith((EnabledKey, "true"), (CodesKey, new string('k', 129)));

        DatabaseStartupTests.AssertStartupFailsNamingKey(exception, CodesKey);
    }

    [Fact]
    public void TheStartupFailure_NeverPrintsTheCode()
    {
        // 15 caracteres: la spec cita «corto-secreto-prueba», que mide 20 y por tanto sería válido (mínimo 16).
        const string ShortSecret = "corto-secreto-1";

        var exception = StartWith((EnabledKey, "true"), (CodesKey, ShortSecret));

        DatabaseStartupTests.AssertStartupFailsNamingKey(exception, CodesKey);
        Assert.DoesNotContain(ShortSecret, StartupFailure.AllMessages(exception), StringComparison.Ordinal);
        Assert.DoesNotContain(ShortSecret, StartupFailure.Describe(exception), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(128)]
    public Task ACodeAtTheLengthEdges_Starts(int length) => AssertStartsAsync((EnabledKey, "true"), (CodesKey, new string('k', length)));

    [Fact]
    public Task SeveralValidCodes_Start() => AssertStartsAsync((EnabledKey, "true"), (CodesKey, $"{ValidCode},otro-codigo-prueba-9876543210"));

    [Fact]
    public Task CodesAreTrimmedBeforeTheirLengthIsChecked() => AssertStartsAsync((EnabledKey, "true"), (CodesKey, $"  {ValidCode}  ,\totro-codigo-prueba-9876543210 "));

    [Fact]
    public Task DuplicatedCodes_DoNotStopTheStartup() => AssertStartsAsync((EnabledKey, "true"), (CodesKey, $"{ValidCode},{ValidCode}"));

    [Theory]
    [InlineData("corto")]
    [InlineData("corto,,")]
    [InlineData("")]
    public Task AClosedRegistration_IgnoresTheCodes(string codes) => AssertStartsAsync((EnabledKey, "false"), (CodesKey, codes));

    [Fact]
    public void ParseInviteCodes_SplitsOnCommasAndTrimsEachElement()
    {
        var options = new RegistrationOptions { InviteCodes = $" {ValidCode} ,otro-codigo-prueba-9876543210" };

        Assert.Equal([ValidCode, "otro-codigo-prueba-9876543210"], options.ParseInviteCodes());
    }

    [Fact]
    public void ParseInviteCodes_KeepsBlankElementsSoTheValidatorCanRejectThem()
    {
        var options = new RegistrationOptions { InviteCodes = $"{ValidCode}, ," };

        Assert.Equal([ValidCode, string.Empty, string.Empty], options.ParseInviteCodes());
    }
}
