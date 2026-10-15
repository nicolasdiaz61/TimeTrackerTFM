using System.Diagnostics.CodeAnalysis;

namespace TimeTracker.Web.Timeline;

/// <summary>Tipo de franja de la barra de un día; corresponde a los tipos de intervalo de la API.</summary>
public enum SegmentKind
{
    Work,
    Break,
    MealBreak,
}

public static class SegmentKinds
{
    /// <summary>Interpreta el texto de la API; un tipo desconocido (API más reciente que la Web) no se dibuja.</summary>
    public static bool TryParse(string type, [NotNullWhen(true)] out SegmentKind? kind)
    {
        kind = type switch
        {
            "Work" => SegmentKind.Work,
            "Break" => SegmentKind.Break,
            "MealBreak" => SegmentKind.MealBreak,
            _ => null,
        };
        return kind is not null;
    }

    public static string Label(SegmentKind kind) => kind switch
    {
        SegmentKind.Work => "Trabajo",
        SegmentKind.Break => "Pausa",
        _ => "Comida",
    };

    /// <summary>Clase CSS de la franja (los colores viven en app.css).</summary>
    public static string CssClass(SegmentKind kind) => kind switch
    {
        SegmentKind.Work => "seg-work",
        SegmentKind.Break => "seg-break",
        _ => "seg-meal",
    };
}
