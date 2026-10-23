using System.Text;

using TimeTracker.Web.Assistant;

namespace TimeTracker.Web.Tests;

public class CsvBuilderTests
{
    private static string Text(byte[] bytes)
    {
        Assert.True(bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }), "Falta el BOM UTF-8.");
        return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
    }

    [Fact]
    public void Build_StartsWithBomAndUsesSemicolonAndCrlf()
    {
        var csv = Text(CsvBuilder.Build(["Persona", "Pausas"], [["Ana", "12"], ["Luis", "8"]]));

        Assert.Equal("Persona;Pausas\r\nAna;12\r\nLuis;8\r\n", csv);
    }

    [Fact]
    public void Build_Empty_ReturnsOnlyTheBom()
    {
        Assert.Equal(string.Empty, Text(CsvBuilder.Build([], [])));
    }

    [Fact]
    public void Build_HeaderOnly_ReturnsOneLine()
    {
        Assert.Equal("A;B\r\n", Text(CsvBuilder.Build(["A", "B"], [])));
    }

    [Theory]
    [InlineData("a;b", "\"a;b\"")]
    [InlineData("di \"hola\"", "\"di \"\"hola\"\"\"")]
    [InlineData("l1\nl2", "\"l1\nl2\"")]
    [InlineData("l1\r\nl2", "\"l1\r\nl2\"")]
    [InlineData("a,b", "a,b")]
    public void Escape_QuotesPerRfc4180(string cell, string expected) => Assert.Equal(expected, CsvBuilder.Escape(cell));

    [Theory]
    [InlineData("=SUM(A1:A2)", "'=SUM(A1:A2)")]
    [InlineData("+cmd", "'+cmd")]
    [InlineData("-cmd", "'-cmd")]
    [InlineData("@SUM(1)", "'@SUM(1)")]
    [InlineData("\tx", "'\tx")]
    [InlineData("=1;2", "\"'=1;2\"")]
    [InlineData("-1+2", "'-1+2")]
    public void Escape_PrefixesFormulaCharacters(string cell, string expected) => Assert.Equal(expected, CsvBuilder.Escape(cell));

    [Theory]
    [InlineData("-5")]
    [InlineData("+5")]
    [InlineData("-5,25")]
    [InlineData("+12.5")]
    [InlineData("42")]
    [InlineData("8 h 30 min")]
    public void Escape_KeepsPlainNumbersUntouched(string cell) => Assert.Equal(cell, CsvBuilder.Escape(cell));

    [Fact]
    public void FileName_UsesTheTimestamp()
    {
        Assert.Equal("asistente-tabla-20261023-0905.csv", CsvBuilder.FileName(new DateTime(2026, 10, 23, 9, 5, 30)));
    }
}
