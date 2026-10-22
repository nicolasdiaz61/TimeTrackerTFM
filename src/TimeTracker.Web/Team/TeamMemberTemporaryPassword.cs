using System.Net;

using TimeTracker.Web.Http;

namespace TimeTracker.Web.Team;

/// <summary>
/// Regeneración de la contraseña temporal desde /equipo sin dependencia del navegador: confirmación previa, llamada con el
/// identificador de la persona (sin compañía ni contraseñas), contraseña mostrada una sola vez y traducción de los estados HTTP.
/// Como la lista no indica quién tiene el cambio pendiente, la acción se ofrece a todos los reportes directos y el
/// <c>409</c> de la API dice si la persona ya cambió su contraseña.
/// </summary>
public sealed class TeamMemberTemporaryPassword(IEmployeesApi api)
{
    public const string NotFoundMessage = "Esa persona ya no forma parte de su equipo.";
    public const string NoPermissionMessage = "No tiene permiso para regenerar contraseñas temporales.";
    public const string TooManyRequestsMessage = "Demasiadas regeneraciones seguidas.";

    private readonly FormFeedback feedback = new();

    private Guid? pendingMemberId;

    public string? PendingName { get; private set; }

    public bool ConfirmationPending => pendingMemberId is not null;

    public TemporaryPasswordReveal Reveal { get; } = new();

    public bool IsBusy { get; private set; }

    public string? ErrorMessage => feedback.Message;

    /// <summary>Pide la confirmación previa para esa persona; no llama a la API.</summary>
    public void Request(Guid memberId, string displayName)
    {
        ArgumentNullException.ThrowIfNull(displayName);

        if (IsBusy)
        {
            return;
        }

        Reveal.Dismiss();
        feedback.Clear();
        pendingMemberId = memberId;
        PendingName = displayName;
    }

    public void Cancel()
    {
        if (IsBusy)
        {
            return;
        }

        pendingMemberId = null;
        PendingName = null;
    }

    public async Task ConfirmAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy || pendingMemberId is not { } memberId)
        {
            return;
        }

        var name = PendingName ?? string.Empty;
        pendingMemberId = null;
        PendingName = null;
        IsBusy = true;
        feedback.Clear();
        try
        {
            var result = await api.RegenerateTemporaryPasswordAsync(memberId, cancellationToken);
            if (result.Value is { } regenerated)
            {
                Reveal.Show(name, regenerated.TemporaryPassword);
                return;
            }

            // El 401 lo trata el BearerTokenHandler (sesión cerrada y redirección a /login): aquí no se muestra.
            if (result.Error is { StatusCode: not HttpStatusCode.Unauthorized } failure)
            {
                feedback.ShowFailure(failure, TooManyRequestsMessage, Specific);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string? Specific(ApiFailure failure) => failure.StatusCode switch
    {
        HttpStatusCode.NotFound => NotFoundMessage,
        HttpStatusCode.Forbidden => NoPermissionMessage,
        HttpStatusCode.Conflict => failure.Message,
        _ => null,
    };
}
