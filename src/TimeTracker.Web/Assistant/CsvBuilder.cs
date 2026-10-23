using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TimeTracker.Web.Assistant;

/// <summary>
/// Genera el CSV de una tabla del asistente: UTF-8 con BOM (Excel), separador «;», saltos CRLF, comillado RFC 4180 y
/// protección frente a inyección de fórmulas (una celda que empieza por = + - @ tabulador o CR lleva un apóstrofo
/// delante, salvo los números simples).
/// </summary>
public static partial class CsvBuilder
{
    private const char Separator = ';';

    public static byte[] Build(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var text = new StringBuilder();
        if (headers.Count > 0)
        {
            AppendLine(text, headers);
            foreach (var row in rows)
            {
                AppendLine(text, row);
            }
        }

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text.ToString())];
    }

    public static string Escape(string cell)
    {
        var value = cell;
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' && !PlainNumber().IsMatch(value))
        {
            value = "'" + value;
        }

        return value.IndexOfAny([Separator, '"', '\r', '\n']) >= 0
            ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : value;
    }

    public static string FileName(DateTime now) => string.Create(CultureInfo.InvariantCulture, $"asistente-tabla-{now:yyyyMMdd-HHmm}.csv");

    private static void AppendLine(StringBuilder text, IReadOnlyList<string> cells)
    {
        text.AppendJoin(Separator, cells.Select(Escape)).Append("\r\n");
    }

    [GeneratedRegex(@"^[+-]?\d+([.,]\d+)?$")]
    private static partial Regex PlainNumber();
}
