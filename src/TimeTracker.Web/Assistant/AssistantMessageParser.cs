using System.Text;
using System.Text.RegularExpressions;

namespace TimeTracker.Web.Assistant;

public abstract record AssistantBlock;

public sealed record ParagraphBlock(string Text) : AssistantBlock;

public sealed record TableBlock(IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows) : AssistantBlock;

/// <summary>
/// Divide la respuesta del asistente en párrafos y tablas (solo presentación). Reconoce únicamente tablas de tuberías
/// al estilo GitHub; todo lo demás queda como texto. La salida son datos, nunca marcado: la salida del modelo no es
/// de confianza (AGENTS.md §6) y la página la pinta siempre con expresiones Razor, que escapan el contenido.
/// </summary>
public static partial class AssistantMessageParser
{
    public const int MaxRows = 200;
    public const int MaxColumns = 20;
    public const int MaxCellLength = 200;

    public static IReadOnlyList<AssistantBlock> Parse(string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var blocks = new List<AssistantBlock>();
        var paragraph = new List<string>();
        var index = 0;
        while (index < lines.Length)
        {
            if (TryReadTable(lines, index, out var table, out var next))
            {
                FlushParagraph(blocks, paragraph);
                blocks.Add(table!);
                index = next;
                continue;
            }

            paragraph.Add(lines[index]);
            index++;
        }

        FlushParagraph(blocks, paragraph);
        return blocks;
    }

    private static void FlushParagraph(List<AssistantBlock> blocks, List<string> paragraph)
    {
        var text = string.Join('\n', paragraph).Trim('\n', ' ', '\t');
        paragraph.Clear();
        if (text.Length > 0)
        {
            blocks.Add(new ParagraphBlock(text));
        }
    }

    private static bool TryReadTable(string[] lines, int start, out TableBlock? table, out int next)
    {
        table = null;
        next = start;
        if (start + 1 >= lines.Length || !lines[start].Contains('|', StringComparison.Ordinal) || !lines[start + 1].Contains('|', StringComparison.Ordinal))
        {
            return false;
        }

        var headers = SplitCells(lines[start]);
        var separator = SplitCells(lines[start + 1]);
        if (headers.Count > MaxColumns || separator.Count != headers.Count || !separator.All(cell => SeparatorCell().IsMatch(cell)))
        {
            return false;
        }

        var rows = new List<IReadOnlyList<string>>();
        var index = start + 2;
        while (index < lines.Length && lines[index].Contains('|', StringComparison.Ordinal))
        {
            if (rows.Count == MaxRows)
            {
                return false;
            }

            rows.Add(Fit(SplitCells(lines[index]), headers.Count));
            index++;
        }

        table = new TableBlock(headers, rows);
        next = index;
        return true;
    }

    private static List<string> Fit(List<string> cells, int width)
    {
        if (cells.Count > width)
        {
            cells.RemoveRange(width, cells.Count - width);
        }

        while (cells.Count < width)
        {
            cells.Add(string.Empty);
        }

        return cells;
    }

    private static List<string> SplitCells(string line)
    {
        var text = line.Trim();
        var cells = new List<string>();
        var current = new StringBuilder();
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 1 < text.Length && text[i + 1] == '|')
            {
                current.Append('|');
                i++;
            }
            else if (text[i] == '|')
            {
                cells.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(text[i]);
            }
        }

        // Si la línea no termina en tubería sin escapar, queda una última celda pendiente.
        var endsWithPipe = text.EndsWith('|') && !text.EndsWith("\\|", StringComparison.Ordinal);
        if (!endsWithPipe)
        {
            cells.Add(current.ToString());
        }

        // La tubería inicial es opcional y produce una celda vacía al principio.
        if (text.StartsWith('|') && cells.Count > 0)
        {
            cells.RemoveAt(0);
        }

        return cells.ConvertAll(cell => Truncate(cell.Trim()));
    }

    private static string Truncate(string cell) => cell.Length <= MaxCellLength ? cell : cell[..MaxCellLength];

    [GeneratedRegex(@"^:?-+:?$")]
    private static partial Regex SeparatorCell();
}
