using System.Net;

using TimeTracker.Web.Http;

namespace TimeTracker.Web.Team;

/// <summary>
/// Estado de /equipo/alta sin dependencia del navegador: mapeo a la petición (sin identificador de compañía), responsable elegido
/// solo de la lista de <c>GET /api/team/members</c>, contraseña temporal mostrada una sola vez y traducción de los estados HTTP.
/// Solo hay alta: sin baja ni edición. Los permisos reales los aplica la API.
/// </summary>
public sealed class TeamMemberCreationForm(IEmployeesApi api, bool isManager)
{
    public const string NoPermissionMessage = "No tiene permiso para dar de alta personas.";
    public const string TooManyRequestsMessage = "Demasiadas altas seguidas.";

    private static readonly string[] ShownFields = ["displayName", "email", "role", "managerId"];

    private readonly FormFeedback feedback = new();

    public string DisplayName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    /// <summary><c>Employee</c> o <c>Manager</c>, tal como lo espera la API.</summary>
    public string Role { get; set; } = "Employee";

    /// <summary>Responsable elegido; <see langword="null"/> deja que la API asigne a quien da de alta.</summary>
    public Guid? ManagerId { get; private set; }

    public IReadOnlyList<TeamMemberDto> Candidates { get; private set; } = [];

    public TemporaryPasswordReveal Reveal { get; } = new();

    public bool IsBusy { get; private set; }

    public string? ErrorMessage => feedback.Message;

    public IReadOnlyList<string> ErrorsFor(string field) => feedback.ErrorsFor(field);

    public IReadOnlyList<string> OtherErrors => feedback.OtherErrors(ShownFields);

    /// <summary>Los únicos responsables elegibles son las personas que devolvió el equipo del propio Manager.</summary>
    public void SetCandidates(IReadOnlyList<TeamMemberDto> members)
    {
        ArgumentNullException.ThrowIfNull(members);

        Candidates = members;
        if (ManagerId is { } chosen && members.All(member => member.Id != chosen))
        {
            ManagerId = null;
        }
    }

    public void SelectManager(Guid? managerId)
        => ManagerId = managerId is { } id && Candidates.Any(member => member.Id == id) ? id : null;

    public async Task SubmitAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return;
        }

        Reveal.Dismiss();
        feedback.Clear();
        if (!isManager)
        {
            feedback.Show(NoPermissionMessage);
            return;
        }

        IsBusy = true;
        try
        {
            var result = await api.CreateAsync(new CreateEmployeeRequestDto(DisplayName.Trim(), Email.Trim(), Role, ManagerId), cancellationToken);
            if (result.Value is { } created)
            {
                Reveal.Show(created.DisplayName, created.TemporaryPassword);
                DisplayName = string.Empty;
                Email = string.Empty;
                Role = "Employee";
                ManagerId = null;
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
        HttpStatusCode.Forbidden => NoPermissionMessage,
        HttpStatusCode.Conflict => failure.Message,
        _ => null,
    };
}
