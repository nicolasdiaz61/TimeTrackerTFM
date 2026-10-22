using TimeTracker.Application.Authentication;

namespace TimeTracker.Application.Tests;

/// <summary>Contraseña temporal (DD-21): 16 caracteres o más, de un alfabeto sin ambigüedades, que cumple la política y no se repite.</summary>
public class TemporaryPasswordGeneratorTests
{
    private const string Ambiguous = "0O1lI";

    [Fact]
    public void Generate_ReturnsAtLeastSixteenCharactersThatMeetThePasswordPolicy()
    {
        var generator = new TemporaryPasswordGenerator();

        var passwords = Enumerable.Range(0, 50).Select(_ => generator.Generate()).ToList();

        Assert.Equal(50, passwords.Count);
        Assert.All(passwords, password =>
        {
            Assert.True(password.Length >= 16, "La contraseña temporal debe tener al menos 16 caracteres.");
            Assert.Null(PasswordPolicy.Validate(password, "temporaryPassword"));
        });
    }

    [Fact]
    public void Generate_ProducesDistinctValues_AndNeverUsesAmbiguousCharacters()
    {
        var generator = new TemporaryPasswordGenerator();

        var passwords = Enumerable.Range(0, 50).Select(_ => generator.Generate()).ToList();

        Assert.Equal(50, passwords.Distinct(StringComparer.Ordinal).Count());
        Assert.All(passwords, password => Assert.All(password, character =>
        {
            Assert.True(char.IsAsciiLetterOrDigit(character), $"Carácter no alfanumérico: {character}");
            Assert.DoesNotContain(character, Ambiguous);
        }));
    }

    [Fact]
    public void Generate_UsesTheWholeAlphabet_NotAFixedSubset()
    {
        var generator = new TemporaryPasswordGenerator();

        var used = Enumerable.Range(0, 400).SelectMany(_ => generator.Generate()).Distinct().Count();

        // 400 contraseñas de 16 caracteres sobre un alfabeto de 57 símbolos los usan todos con certeza práctica.
        Assert.Equal(TemporaryPasswordGenerator.Alphabet.Length, used);
    }
}
