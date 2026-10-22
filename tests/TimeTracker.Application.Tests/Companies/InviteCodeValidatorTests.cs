using System.Security.Cryptography;
using System.Text;

using TimeTracker.Application.Companies;

namespace TimeTracker.Application.Tests.Companies;

/// <summary>Código de invitación secreto (DD-19): comparación en tiempo constante recorriendo todos los códigos, sin revelar cuál se aceptó.</summary>
public class InviteCodeValidatorTests
{
    private const string First = "codigo-prueba-0123456789";
    private const string Second = "otro-codigo-prueba-9876543210";

    [Fact]
    public void IsValid_AcceptsEachConfiguredCode()
    {
        var validator = new InviteCodeValidator([First, Second]);

        Assert.True(validator.IsValid(First));
        Assert.True(validator.IsValid(Second));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("codigo-prueba-0123456780")]
    [InlineData("CODIGO-PRUEBA-0123456789")]
    [InlineData("codigo-prueba-012345678")]
    [InlineData("codigo-prueba-01234567890")]
    public void IsValid_RejectsAbsentBlankAndWrongCodes(string? candidate)
    {
        var validator = new InviteCodeValidator([First, Second]);

        Assert.False(validator.IsValid(candidate));
    }

    [Fact]
    public void IsValid_RejectsACandidateOfMoreThan128Characters_EvenWhenItStartsWithAValidCode()
    {
        var validator = new InviteCodeValidator([First]);

        Assert.False(validator.IsValid(First + new string('x', 129 - First.Length)));
        Assert.False(validator.IsValid(new string('x', 129)));
    }

    [Fact]
    public void IsValid_RejectsACandidateOverTheLimitEvenIfItEqualsAConfiguredOversizedCode()
    {
        // El arranque ya impide configurar un código de más de 128, pero el validador no depende de ello (defensa en profundidad).
        var oversized = new string('z', 130);

        Assert.False(new InviteCodeValidator([oversized]).IsValid(oversized));
    }

    [Fact]
    public void IsValid_AcceptsACodeOfExactly128Characters()
    {
        var code = new string('k', 128);
        var validator = new InviteCodeValidator([code]);

        Assert.True(validator.IsValid(code));
        Assert.False(validator.IsValid(new string('k', 127)));
    }

    [Fact]
    public void IsValid_TrimsTheCandidateAndTheConfiguredCodes_ButNeverMatchesAnEmptyConfiguredEntry()
    {
        var validator = new InviteCodeValidator([$"  {First}  ", string.Empty]);

        Assert.True(validator.IsValid($" {First} "));
        Assert.False(validator.IsValid(string.Empty));
        Assert.False(validator.IsValid("   "));
    }

    [Fact]
    public void IsValid_DoesNotConsumeTheCode()
    {
        var validator = new InviteCodeValidator([First]);

        Assert.True(validator.IsValid(First));
        Assert.True(validator.IsValid(First));
    }

    [Fact]
    public void IsValid_WithoutConfiguredCodes_RejectsEverything()
        => Assert.False(new InviteCodeValidator([]).IsValid(First));

    [Fact]
    public void MatchesAny_VisitsEveryCodeEvenWhenTheFirstOneMatches()
    {
        var hashes = new[] { Hash(First), Hash(Second), Hash("tercer-codigo-prueba-111111") };
        var visited = new List<byte[]>();

        var matched = InviteCodeValidator.MatchesAny(Hash(First), hashes, (candidate, code) =>
        {
            visited.Add(code);
            return CryptographicOperations.FixedTimeEquals(candidate, code);
        });

        Assert.True(matched);
        Assert.Equal(hashes, visited);
    }

    [Fact]
    public void MatchesAny_VisitsEveryCodeWhenNoneMatches_AndReportsNoMatch()
    {
        var hashes = new[] { Hash(First), Hash(Second) };
        var visits = 0;

        var matched = InviteCodeValidator.MatchesAny(Hash("ninguno-de-los-codigos-1"), hashes, (candidate, code) =>
        {
            visits++;
            return CryptographicOperations.FixedTimeEquals(candidate, code);
        });

        Assert.False(matched);
        Assert.Equal(2, visits);
    }

    [Fact]
    public void MatchesAny_IsTrueWhenOnlyTheLastCodeMatches()
    {
        var hashes = new[] { Hash(First), Hash(Second) };

        Assert.True(InviteCodeValidator.MatchesAny(Hash(Second), hashes, (left, right) => CryptographicOperations.FixedTimeEquals(left, right)));
    }

    [Fact]
    public void TheValidator_ExposesOnlyAcceptedOrRejected_NeverTheCodeOrItsPosition()
    {
        // La forma pública del validador es un booleano: no hay ni valor ni índice que registrar (Q-12).
        var method = typeof(IInviteCodeValidator).GetMethods().Single();

        Assert.Equal("IsValid", method.Name);
        Assert.Equal(typeof(bool), method.ReturnType);
    }

    private static byte[] Hash(string code) => SHA256.HashData(Encoding.UTF8.GetBytes(code));
}
