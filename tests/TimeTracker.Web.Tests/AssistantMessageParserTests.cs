using System.Text;

using TimeTracker.Web.Assistant;

namespace TimeTracker.Web.Tests;

public class AssistantMessageParserTests
{
    private static TableBlock SingleTable(string text) => Assert.Single(AssistantMessageParser.Parse(text).OfType<TableBlock>());

    [Fact]
    public void Parse_ValidTable_ReturnsHeadersAndRows()
    {
        var blocks = AssistantMessageParser.Parse("| Persona | Pausas |\n|---|---|\n| Ana | 12 |\n| Luis | 8 |");

        var table = Assert.IsType<TableBlock>(Assert.Single(blocks));
        Assert.Equal(["Persona", "Pausas"], table.Headers);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(["Ana", "12"], table.Rows[0]);
        Assert.Equal(["Luis", "8"], table.Rows[1]);
    }

    [Fact]
    public void Parse_WithoutLeadingAndTrailingPipes_IsRecognised()
    {
        var table = SingleTable("Persona | Pausas\n--- | ---\nAna | 12\r\nLuis | 8");

        Assert.Equal(["Persona", "Pausas"], table.Headers);
        Assert.Equal(["Luis", "8"], table.Rows[1]);
    }

    [Fact]
    public void Parse_SeparatorWithAlignmentColons_IsRecognised()
    {
        var table = SingleTable("| A | B | C |\n|:---|:---:|---:|\n| 1 | 2 | 3 |");

        Assert.Equal(3, table.Headers.Count);
    }

    [Fact]
    public void Parse_RaggedRows_ArePaddedAndTruncatedToTheHeaderWidth()
    {
        var table = SingleTable("| A | B | C |\n|---|---|---|\n| 1 |\n| 1 | 2 | 3 | 4 | 5 |");

        Assert.Equal(["1", "", ""], table.Rows[0]);
        Assert.Equal(["1", "2", "3"], table.Rows[1]);
    }

    [Fact]
    public void Parse_EscapedPipe_StaysInsideTheCell()
    {
        var table = SingleTable("| A | B |\n|---|---|\n| a \\| b | c |");

        Assert.Equal(["a | b", "c"], table.Rows[0]);
    }

    [Fact]
    public void Parse_TextWithoutTable_IsOneParagraph()
    {
        var blocks = AssistantMessageParser.Parse("Trabajó 8 h.\nY descansó 30 min.");

        var paragraph = Assert.IsType<ParagraphBlock>(Assert.Single(blocks));
        Assert.Equal("Trabajó 8 h.\nY descansó 30 min.", paragraph.Text);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n  ")]
    public void Parse_EmptyText_ReturnsNoBlocks(string text) => Assert.Empty(AssistantMessageParser.Parse(text));

    [Fact]
    public void Parse_PipesWithoutSeparatorLine_AreNotATable()
    {
        var blocks = AssistantMessageParser.Parse("a | b\nc | d");

        Assert.IsType<ParagraphBlock>(Assert.Single(blocks));
    }

    [Fact]
    public void Parse_SeparatorWithDifferentColumnCount_IsNotATable()
    {
        var blocks = AssistantMessageParser.Parse("| A | B |\n|---|\n| 1 | 2 |");

        Assert.IsType<ParagraphBlock>(Assert.Single(blocks));
    }

    [Fact]
    public void Parse_TextBeforeAndAfter_IsKeptAsParagraphs()
    {
        var blocks = AssistantMessageParser.Parse("Resumen:\n\n| A | B |\n|---|---|\n| 1 | 2 |\n\nFin.");

        Assert.Equal(3, blocks.Count);
        Assert.Equal("Resumen:", Assert.IsType<ParagraphBlock>(blocks[0]).Text);
        Assert.IsType<TableBlock>(blocks[1]);
        Assert.Equal("Fin.", Assert.IsType<ParagraphBlock>(blocks[2]).Text);
    }

    [Fact]
    public void Parse_TwoTables_AreBothRecognised()
    {
        var blocks = AssistantMessageParser.Parse("| A | B |\n|---|---|\n| 1 | 2 |\n\nOtra:\n| C | D |\n|---|---|\n| 3 | 4 |");

        Assert.Equal(2, blocks.OfType<TableBlock>().Count());
        Assert.Equal("Otra:", Assert.Single(blocks.OfType<ParagraphBlock>()).Text);
    }

    [Fact]
    public void Parse_HeaderOnly_IsATableWithoutRows()
    {
        var table = SingleTable("| A | B |\n|---|---|");

        Assert.Empty(table.Rows);
    }

    [Fact]
    public void Parse_MaxRows_IsAccepted_OneMoreIsPlainText()
    {
        static string Build(int rows)
        {
            var text = new StringBuilder("| A | B |\n|---|---|");
            for (var i = 0; i < rows; i++)
            {
                text.Append("\n| x | y |");
            }

            return text.ToString();
        }

        Assert.Equal(AssistantMessageParser.MaxRows, SingleTable(Build(AssistantMessageParser.MaxRows)).Rows.Count);
        Assert.Empty(AssistantMessageParser.Parse(Build(AssistantMessageParser.MaxRows + 1)).OfType<TableBlock>());
    }

    [Fact]
    public void Parse_MaxColumns_IsAccepted_OneMoreIsPlainText()
    {
        static string Build(int columns) => $"{string.Join('|', Enumerable.Repeat("h", columns))}\n{string.Join('|', Enumerable.Repeat("---", columns))}\n{string.Join('|', Enumerable.Repeat("v", columns))}";

        Assert.Equal(AssistantMessageParser.MaxColumns, SingleTable(Build(AssistantMessageParser.MaxColumns)).Headers.Count);
        Assert.Empty(AssistantMessageParser.Parse(Build(AssistantMessageParser.MaxColumns + 1)).OfType<TableBlock>());
    }

    [Fact]
    public void Parse_LongCell_IsTruncated()
    {
        var table = SingleTable($"| A | B |\n|---|---|\n| {new string('x', 500)} | y |");

        Assert.Equal(AssistantMessageParser.MaxCellLength, table.Rows[0][0].Length);
    }

    [Fact]
    public void Parse_HtmlInCells_IsKeptAsLiteralText()
    {
        var table = SingleTable("| <b>A</b> | B |\n|---|---|\n| <script>alert(1)</script> | <img src=x onerror=alert(1)> |");

        Assert.Equal("<script>alert(1)</script>", table.Rows[0][0]);
        Assert.Equal("<b>A</b>", table.Headers[0]);
    }
}
