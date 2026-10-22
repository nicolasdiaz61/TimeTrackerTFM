namespace TimeTracker.ArchitectureTests;

/// <summary>
/// El quitacomentarios compartido por las pruebas de arquitectura respeta los literales: un «//» dentro de una cadena, de una cadena
/// literal, de una cadena en bruto o de un carácter no abre un comentario, y el contenido de las cadenas se conserva (los patrones
/// buscan literales como «"cid"»).
/// </summary>
public class SourceTextTests
{
    [Theory]
    [InlineData("var a = 1; // comentario", "var a = 1; ")]
    [InlineData("// toda la línea\nvar a = 1;", "\nvar a = 1;")]
    [InlineData("var a = 1; /* bloque */ var b = 2;", "var a = 1;  var b = 2;")]
    public void RemovesLineAndBlockComments(string source, string expected)
        => Assert.Equal(expected, SourceText.StripComments(source));

    [Fact]
    public void BlockCommentsKeepTheirLineBreaks_SoLineNumbersDoNotShift()
        => Assert.Equal("a\n\n\nb", SourceText.StripComments("a\n/* uno\ndos */\nb"));

    [Theory]
    [InlineData("var url = \"http://example.test\"; // fin", "var url = \"http://example.test\"; ")]
    [InlineData("var s = \"a\\\"//b\"; // fin", "var s = \"a\\\"//b\"; ")]
    [InlineData("var s = @\"c:\\\\x // y \"\" // z\"; // fin", "var s = @\"c:\\\\x // y \"\" // z\"; ")]
    [InlineData("var s = $\"{a} // {b}\"; // fin", "var s = $\"{a} // {b}\"; ")]
    [InlineData("var s = $@\"{a} // {b}\"; // fin", "var s = $@\"{a} // {b}\"; ")]
    [InlineData("var s = \"\"\"a // b \" c\"\"\"; // fin", "var s = \"\"\"a // b \" c\"\"\"; ")]
    [InlineData("var s = \"\"; // fin", "var s = \"\"; ")]
    [InlineData("var p = @\"c:\\dir\\\"; // fin", "var p = @\"c:\\dir\\\"; ")]
    public void DoesNotTreatSlashesInsideStringLiteralsAsComments(string source, string expected)
        => Assert.Equal(expected, SourceText.StripComments(source));

    [Theory]
    [InlineData("var c = '\"'; // fin \"x\"", "var c = '\"'; ")]
    [InlineData("var c = '\\''; var s = \"//\"; // fin", "var c = '\\''; var s = \"//\"; ")]
    [InlineData("var c = '/'; // fin", "var c = '/'; ")]
    public void CharacterLiteralsDoNotOpenStringsOrComments(string source, string expected)
        => Assert.Equal(expected, SourceText.StripComments(source));

    [Fact]
    public void AStringMentionedInsideACommentIsRemovedWithIt()
        => Assert.Equal("var a = 1; ", SourceText.StripComments("var a = 1; // usa \"cid\" aquí"));

    [Fact]
    public void AnUnterminatedStringAtTheEndOfTheSource_DoesNotLoop_AndIsKept()
        => Assert.Equal("var s = \"abc", SourceText.StripComments("var s = \"abc"));

    [Fact]
    public void EmptySource_IsEmpty()
        => Assert.Equal(string.Empty, SourceText.StripComments(string.Empty));
}
