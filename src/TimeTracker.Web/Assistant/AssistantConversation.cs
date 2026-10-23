using System.Net;

using TimeTracker.Web.Http;

namespace TimeTracker.Web.Assistant;

public enum TurnRole
{
    User,
    Assistant,
}

public enum CounterLevel
{
    Normal,
    Warning,
    Danger,
}

public sealed record ConversationTurn(TurnRole Role, string Text);

/// <summary>
/// Conversación en memoria de la página /asistente, sin dependencia del navegador: turnos, historial de la petición,
/// validación del mensaje, estado ocupado y traducción de los errores de la API a mensajes en español (DD-17).
/// No contiene reglas de negocio: los límites replican los que la API impone igualmente.
/// </summary>
public sealed class AssistantConversation
{
    public const int MaxMessageLength = 500;
    public const int MaxScreenTurns = 500;
    public const int MaxHistoryTurns = 10;
    public const int MaxHistoryCharacters = 8000;
    public const int MaxHistoryTurnLength = 2000;

    public const string EmptyMessageError = "Escriba un mensaje para el asistente.";
    public const string TooLongMessageError = "El mensaje no puede superar los 500 caracteres.";
    public const string InvalidMessageFallback = "El mensaje no es válido. Revise el texto e inténtelo de nuevo.";
    public const string TooManyRequestsFallback = "Demasiadas consultas al asistente. Inténtelo de nuevo en unos minutos.";
    public const string UnavailableFallback = "El asistente no está disponible en este momento. Inténtelo de nuevo más tarde.";

    private const int WarningThreshold = 80;
    private const string ManagerExample = "¿Quién hace más pausas que el promedio?";

    private static readonly string[] EmployeeExamples =
    [
        "¿Cuántas horas trabajé esta semana?",
        "¿Cuál fue mi última entrada?",
        "¿Cuántas horas extra llevo este mes?",
    ];

    private readonly List<ConversationTurn> turns = [];

    public IReadOnlyList<ConversationTurn> Turns => turns;

    public bool IsBusy { get; private set; }

    /// <summary>Error del último envío; se limpia al iniciar otro.</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>Aviso persistente del 503: sigue visible hasta que llega una respuesta correcta.</summary>
    public string? Notice { get; private set; }

    /// <summary>Preguntas de ejemplo del estado vacío; el responsable ve además una sobre pausas de su equipo.</summary>
    public static IReadOnlyList<string> ExampleQuestions(bool isManager)
        => isManager ? [.. EmployeeExamples[..2], ManagerExample] : EmployeeExamples;

    /// <summary>Longitud del mensaje tal como se enviará (sin espacios en los extremos).</summary>
    public static int MessageLength(string? message) => message?.Trim().Length ?? 0;

    public static CounterLevel CounterLevelFor(int length)
    {
        if (length > MaxMessageLength)
        {
            return CounterLevel.Danger;
        }

        return length >= MaxMessageLength * WarningThreshold / 100 ? CounterLevel.Warning : CounterLevel.Normal;
    }

    /// <summary>Empieza una conversación nueva: descarta turnos, errores, aviso y estado ocupado.</summary>
    public void Reset()
    {
        turns.Clear();
        ErrorMessage = null;
        Notice = null;
        IsBusy = false;
    }

    /// <summary>Motivo en español por el que el mensaje no se puede enviar, o <see langword="null"/> si es válido.</summary>
    public string? Validate(string? message)
    {
        var length = message?.Trim().Length ?? 0;
        if (length == 0)
        {
            return EmptyMessageError;
        }

        return length > MaxMessageLength ? TooLongMessageError : null;
    }

    /// <summary>
    /// Inicia un envío: valida, añade el turno pendiente y devuelve la petición con el historial anterior.
    /// Devuelve <see langword="null"/> (sin llamar a la API) si ya hay un envío en curso o el mensaje no es válido.
    /// </summary>
    public AssistantRequestDto? TryBegin(string? message)
    {
        if (IsBusy)
        {
            return null;
        }

        ErrorMessage = Validate(message);
        if (ErrorMessage is not null)
        {
            return null;
        }

        var text = message!.Trim();
        var request = new AssistantRequestDto(text, BuildHistory());
        Add(new ConversationTurn(TurnRole.User, text));
        IsBusy = true;
        return request;
    }

    public void Complete(string reply)
    {
        Add(new ConversationTurn(TurnRole.Assistant, reply));
        Notice = null;
        ErrorMessage = null;
        IsBusy = false;
    }

    /// <summary>Retira el turno pendiente y traduce el error; <paramref name="retryAfterSeconds"/> es el <c>Retry-After</c> del 429, si llegó.</summary>
    public void Fail(ApiRequestException error, int? retryAfterSeconds = null)
    {
        ArgumentNullException.ThrowIfNull(error);

        if (IsBusy && turns.Count > 0 && turns[^1].Role == TurnRole.User)
        {
            turns.RemoveAt(turns.Count - 1);
        }

        IsBusy = false;
        var detail = error.Message == ApiRequest.GenericErrorMessage ? null : error.Message;
        switch (error.StatusCode)
        {
            case HttpStatusCode.BadRequest:
                ErrorMessage = detail ?? InvalidMessageFallback;
                break;
            case HttpStatusCode.TooManyRequests:
                ErrorMessage = (detail ?? TooManyRequestsFallback) + WaitHint.Describe(retryAfterSeconds);
                break;
            case HttpStatusCode.ServiceUnavailable:
                Notice = detail ?? UnavailableFallback;
                ErrorMessage = null;
                break;
            default:
                ErrorMessage = ApiRequest.GenericErrorMessage;
                break;
        }
    }

    private void Add(ConversationTurn turn)
    {
        turns.Add(turn);
        if (turns.Count > MaxScreenTurns)
        {
            turns.RemoveRange(0, turns.Count - MaxScreenTurns);
        }
    }

    // Los turnos más recientes que caben en 10 turnos y 8000 caracteres, en orden cronológico.
    private List<AssistantTurnDto> BuildHistory()
    {
        var history = new List<AssistantTurnDto>();
        var characters = 0;
        for (var index = turns.Count - 1; index >= 0 && history.Count < MaxHistoryTurns; index--)
        {
            var text = turns[index].Text;
            if (text.Length > MaxHistoryTurnLength)
            {
                text = text[..MaxHistoryTurnLength];
            }

            if (characters + text.Length > MaxHistoryCharacters)
            {
                break;
            }

            characters += text.Length;
            history.Add(new AssistantTurnDto(turns[index].Role == TurnRole.User ? "user" : "assistant", text));
        }

        history.Reverse();
        return history;
    }
}
