using System.Text;

namespace TimeTracker.ArchitectureTests;

/// <summary>Utilidades de texto de código fuente compartidas por las pruebas de arquitectura.</summary>
internal static class SourceText
{
    /// <summary>
    /// Quita los comentarios de línea y de bloque de un fuente C# sin tocar los literales: cadenas normales, interpoladas, literales
    /// (<c>@""</c>), en bruto (<c>""" """</c>) y caracteres conservan su contenido, de modo que un «//» dentro de ellos no abre un
    /// comentario y los patrones siguen viendo valores como <c>"cid"</c>. Conserva los saltos de línea (también los de un comentario de
    /// bloque) para no desplazar las líneas. Es un recorrido léxico mínimo, suficiente para estas pruebas; no interpreta directivas ni
    /// huecos interpolados con cadenas anidadas.
    /// </summary>
    public static string StripComments(string source)
    {
        var result = new StringBuilder(source.Length);
        var i = 0;
        while (i < source.Length)
        {
            var current = source[i];
            var next = i + 1 < source.Length ? source[i + 1] : '\0';
            if (current == '/' && next == '/')
            {
                while (i < source.Length && source[i] != '\n')
                {
                    i++;
                }
            }
            else if (current == '/' && next == '*')
            {
                i = SkipBlockComment(source, i, result);
            }
            else if (current == '"')
            {
                i = CopyString(source, i, result);
            }
            else if (current == '\'')
            {
                i = CopyCharacter(source, i, result);
            }
            else
            {
                result.Append(current);
                i++;
            }
        }

        return result.ToString();
    }

    private static int SkipBlockComment(string source, int start, StringBuilder result)
    {
        var i = start + 2;
        while (i < source.Length && !(source[i] == '*' && i + 1 < source.Length && source[i + 1] == '/'))
        {
            if (source[i] == '\n')
            {
                result.Append('\n');
            }

            i++;
        }

        return Math.Min(i + 2, source.Length);
    }

    private static int CopyString(string source, int start, StringBuilder result)
    {
        var quotes = 0;
        while (start + quotes < source.Length && source[start + quotes] == '"')
        {
            quotes++;
        }

        // Tres comillas o más abren una cadena en bruto y la cierra una racha igual de larga; dos son una cadena vacía.
        if (quotes >= 3)
        {
            return CopyRaw(source, start, quotes, result);
        }

        var verbatim = result.Length > 0 && (result[^1] == '@' || (result.Length > 1 && result[^1] == '$' && result[^2] == '@'));
        result.Append('"');
        var i = start + 1;
        while (i < source.Length)
        {
            var c = source[i];
            result.Append(c);
            i++;
            if (!verbatim && c == '\\' && i < source.Length)
            {
                result.Append(source[i]);
                i++;
            }
            else if (c == '"')
            {
                if (verbatim && i < source.Length && source[i] == '"')
                {
                    result.Append('"');
                    i++;
                    continue;
                }

                break;
            }
        }

        return i;
    }

    private static int CopyRaw(string source, int start, int opening, StringBuilder result)
    {
        result.Append('"', opening);
        var i = start + opening;
        while (i < source.Length)
        {
            if (source[i] == '"')
            {
                var run = 0;
                while (i + run < source.Length && source[i + run] == '"')
                {
                    run++;
                }

                result.Append('"', run);
                i += run;
                if (run >= opening)
                {
                    return i;
                }
            }
            else
            {
                result.Append(source[i]);
                i++;
            }
        }

        return i;
    }

    private static int CopyCharacter(string source, int start, StringBuilder result)
    {
        result.Append('\'');
        var i = start + 1;
        while (i < source.Length)
        {
            var c = source[i];
            result.Append(c);
            i++;
            if (c == '\\' && i < source.Length)
            {
                result.Append(source[i]);
                i++;
            }
            else if (c == '\'' || c == '\n')
            {
                break;
            }
        }

        return i;
    }
}
