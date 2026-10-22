using System.Net;

namespace TimeTracker.Web.Http;

/// <summary>
/// Mensajes de un formulario (general y por campo) y su traducción común de estados HTTP, sin dependencia del navegador.
/// Cada formulario decide los textos propios de sus estados; los demás caen en el mensaje genérico (sin datos técnicos).
/// </summary>
public sealed class FormFeedback
{
    public const string ReviewFieldsMessage = "Revise los campos indicados.";

    private IReadOnlyDictionary<string, string[]> fieldErrors = new Dictionary<string, string[]>();

    public string? Message { get; private set; }

    public void Clear()
    {
        Message = null;
        fieldErrors = new Dictionary<string, string[]>();
    }

    public void Show(string message)
    {
        Message = message;
        fieldErrors = new Dictionary<string, string[]>();
    }

    public void ShowField(string field, string message)
    {
        Message = ReviewFieldsMessage;
        fieldErrors = new Dictionary<string, string[]> { [field] = [message] };
    }

    public IReadOnlyList<string> ErrorsFor(string field) => fieldErrors.TryGetValue(field, out var messages) ? messages : [];

    /// <summary>Errores por campo de campos que la pantalla no muestra: se enseñan en el aviso general.</summary>
    public IReadOnlyList<string> OtherErrors(IReadOnlyCollection<string> shownFields)
        => [.. fieldErrors.Where(pair => !shownFields.Contains(pair.Key)).SelectMany(pair => pair.Value)];

    /// <summary>
    /// Traduce un fallo con los textos comunes: 400 (campos), 429 (esperar) y el genérico; los estados de <paramref name="specific"/>
    /// tienen prioridad y devuelven <see langword="null"/> si el texto específico no aplica.
    /// </summary>
    public void ShowFailure(ApiFailure failure, string tooManyRequestsMessage, Func<ApiFailure, string?>? specific = null)
    {
        ArgumentNullException.ThrowIfNull(failure);
        ArgumentNullException.ThrowIfNull(tooManyRequestsMessage);

        if (specific?.Invoke(failure) is { } message)
        {
            Show(message);
            return;
        }

        switch (failure.StatusCode)
        {
            case HttpStatusCode.BadRequest when failure.FieldErrors.Count > 0:
                Message = ReviewFieldsMessage;
                fieldErrors = failure.FieldErrors;
                break;
            case HttpStatusCode.TooManyRequests:
                Show(tooManyRequestsMessage + WaitHint.Describe(failure.RetryAfterSeconds));
                break;
            default:
                Show(ApiRequest.GenericErrorMessage);
                break;
        }
    }
}
