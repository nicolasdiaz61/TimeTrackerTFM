using System.Globalization;
using System.Text;

namespace TimeTracker.Infrastructure.Assistant;

/// <summary>
/// Sanea un nombre antes de entregarlo al modelo (DD-9): sin caracteres de control ni saltos de línea, espacios
/// consecutivos colapsados y como máximo 60 caracteres. Reduce la inyección indirecta por nombres; no altera el dato guardado.
/// </summary>
internal static class DisplayNameSanitizer
{
    public const int MaxLength = 60;

    public static string Sanitize(string displayName)
    {
        var builder = new StringBuilder(displayName.Length);
        var pendingSpace = false;
        foreach (var character in displayName)
        {
            if (IsSeparator(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(character);
        }

        return Cut(builder.ToString());
    }

    private static bool IsSeparator(char character)
        => char.IsControl(character)
            || char.IsWhiteSpace(character)
            || char.GetUnicodeCategory(character) is UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator;

    private static string Cut(string text)
    {
        if (text.Length <= MaxLength)
        {
            return text;
        }

        var cut = text[..MaxLength];
        if (char.IsHighSurrogate(cut[^1]))
        {
            cut = cut[..^1];
        }

        return cut.TrimEnd();
    }
}
