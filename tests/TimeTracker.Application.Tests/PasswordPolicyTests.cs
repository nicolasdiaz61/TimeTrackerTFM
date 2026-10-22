using TimeTracker.Application.Authentication;

namespace TimeTracker.Application.Tests;

/// <summary>Política de contraseñas (DD-20): de 12 a 128 caracteres, sin reglas de composición y sin tocar los espacios.</summary>
public class PasswordPolicyTests
{
    [Theory]
    [InlineData(11, false)]
    [InlineData(12, true)]
    [InlineData(13, true)]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public void Validate_AppliesTheLengthLimitsInclusively(int length, bool accepted)
    {
        var error = PasswordPolicy.Validate(new string('a', length), "password");

        Assert.Equal(accepted, error is null);
    }

    [Fact]
    public void Validate_WithTwelveLowercaseLettersAndNoDigitsOrSymbols_Accepts()
        => Assert.Null(PasswordPolicy.Validate("abcdefghijkl", "password"));

    [Theory]
    [InlineData("            ")]
    [InlineData("MAYUSCULASSOLO")]
    [InlineData("1234567890123")]
    public void Validate_AddsNoRulesBeyondTheLength(string password)
        => Assert.Null(PasswordPolicy.Validate(password, "password"));

    [Fact]
    public void Validate_CountsOuterSpaces_SoTheyAreNeitherTrimmedNorNormalized()
    {
        // Ocho caracteres visibles y cuatro espacios: solo suma 12 si no se recortan.
        Assert.Null(PasswordPolicy.Validate("  abcdefgh  ", "password"));
        // Con once caracteres y un espacio final sigue sin llegar a 12 contando el espacio: 11 en total.
        Assert.NotNull(PasswordPolicy.Validate("abcdefghij ", "password"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("corta-123")]
    public void Validate_RejectsAbsentEmptyAndShortPasswords(string? password)
        => Assert.NotNull(PasswordPolicy.Validate(password, "password"));

    [Fact]
    public void Validate_NamesTheFieldAndNeverRepeatsThePassword()
    {
        const string Short = "secreta-1";
        var tooLongPassword = new string('x', 129);

        var tooShort = PasswordPolicy.Validate(Short, "newPassword");
        var tooLong = PasswordPolicy.Validate(tooLongPassword, "password");

        Assert.NotNull(tooShort);
        Assert.NotNull(tooLong);
        Assert.Equal("newPassword", tooShort.Field);
        Assert.Equal("password", tooLong.Field);
        Assert.DoesNotContain(Short, tooShort.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(tooLongPassword, tooLong.Message, StringComparison.Ordinal);
        Assert.Equal("La contraseña debe tener entre 12 y 128 caracteres.", tooShort.Message);
        Assert.Equal(tooShort.Message, tooLong.Message);
    }

    [Fact]
    public void Limits_AreTwelveAndTheLoginMaximum()
    {
        Assert.Equal(12, PasswordPolicy.MinimumLength);
        Assert.Equal(LoginUseCase.MaxPasswordLength, PasswordPolicy.MaximumLength);
    }
}
