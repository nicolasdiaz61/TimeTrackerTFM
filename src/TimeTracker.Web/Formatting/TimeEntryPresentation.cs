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
        ClockAction.ClockIn => "Fichar entrada",
        ClockAction.StartBreak => "Iniciar pausa",
        ClockAction.StartMealBreak => "Pausa de comida",
        ClockAction.EndBreak => "Terminar pausa",
        _ => "Fichar salida",
    };
}
