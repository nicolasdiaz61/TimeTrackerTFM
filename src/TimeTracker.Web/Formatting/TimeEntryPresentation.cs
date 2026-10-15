namespace TimeTracker.Web.Formatting;

/// <summary>Acciones de fichaje que ofrece la pantalla; cada una corresponde a un endpoint de la API.</summary>
public enum ClockAction
{
    ClockIn,
    StartBreak,
    StartMealBreak,
    EndBreak,
    ClockOut,
}

/// <summary>
/// Presentación en español del tipo de intervalo, del estado y de las acciones: no valida nada ni calcula tiempos.
/// El estado lo decide la API; aquí solo se elige qué botones mostrar. La validez real la sigue decidiendo el servidor.
/// </summary>
public static class TimeEntryPresentation
{
    // Los valores desconocidos (una API más reciente que la Web) se muestran tal cual, sin romper la pantalla.
    public static string TypeLabel(string type) => type switch
    {
        "Work" => "Trabajo",
        "Break" => "Pausa",
        "MealBreak" => "Comida",
        _ => type,
    };

    public static string StatusLabel(string status) => status switch
    {
        "Idle" => "Sin fichar",
        "Working" => "Trabajando",
        "OnBreak" => "En pausa",
        "OnMealBreak" => "En pausa de comida",
        _ => status,
    };

    public static IReadOnlyList<ClockAction> AvailableActions(string status) => status switch
    {
        "Idle" => [ClockAction.ClockIn],
        "Working" => [ClockAction.StartBreak, ClockAction.StartMealBreak, ClockAction.ClockOut],
        "OnBreak" or "OnMealBreak" => [ClockAction.EndBreak, ClockAction.ClockOut],
        _ => [ClockAction.ClockIn, ClockAction.ClockOut],
    };

    public static string ActionLabel(ClockAction action) => action switch
    {
        ClockAction.ClockIn => "Iniciar jornada",
        ClockAction.StartBreak => "Pausa",
        ClockAction.StartMealBreak => "Comida",
        ClockAction.EndBreak => "Reanudar",
        _ => "Finalizar jornada",
    };

    // Texto para lectores de pantalla: describe lo que hace el botón, no solo su etiqueta corta.
    public static string ActionAriaLabel(ClockAction action) => action switch
    {
        ClockAction.ClockIn => "Iniciar la jornada",
        ClockAction.StartBreak => "Pausar la jornada",
        ClockAction.StartMealBreak => "Pausar la jornada para comer",
        ClockAction.EndBreak => "Reanudar el trabajo",
        _ => "Finalizar la jornada",
    };

    public static string ActionButtonClass(ClockAction action) => action switch
    {
        ClockAction.ClockIn => "btn-primary",
        ClockAction.StartBreak => "btn-warning",
        ClockAction.StartMealBreak => "btn-info",
        ClockAction.EndBreak => "btn-success",
        _ => "btn-danger",
    };

    // Clase CSS del indicador de estado del reproductor (los colores viven en app.css).
    public static string StatusCssClass(string status) => status switch
    {
        "Working" => "status-working",
        "OnBreak" => "status-break",
        "OnMealBreak" => "status-meal",
        _ => "status-idle",
    };
}
