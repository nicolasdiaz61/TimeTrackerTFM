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
        foreach (var rune in displayName.EnumerateRunes())
        {
            if (IsInvisible(rune))
            {
                continue;
            }

            if (IsSeparator(rune))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(rune.ToString());
        }

        return Cut(builder.ToString());
    }

    // Caracteres de formato (ancho cero, controles bidireccionales, BOM) y bloque de etiquetas: no se ven, pero el modelo
    // los lee, así que se eliminan sin dejar espacio.
    private static bool IsInvisible(Rune rune)
        => Rune.GetUnicodeCategory(rune) == UnicodeCategory.Format
            || rune.Value is >= 0xE0000 and <= 0xE007F;

    private static bool IsSeparator(Rune rune)
        => Rune.IsControl(rune)
            || Rune.IsWhiteSpace(rune)
            || Rune.GetUnicodeCategory(rune) is UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator;

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
