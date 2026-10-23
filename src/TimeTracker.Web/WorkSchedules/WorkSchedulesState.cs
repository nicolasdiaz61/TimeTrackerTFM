using System.Globalization;
using System.Net;

using TimeTracker.Web.Http;
using TimeTracker.Web.Team;

namespace TimeTracker.Web.WorkSchedules;

/// <summary>
/// Estado de /jornadas sin dependencia del navegador: listado, formulario de alta y edición (horas como texto «HH:mm»), borrado y
/// asignación de jornadas a las personas del equipo. Los permisos reales los aplica la API; aquí solo se evita llamarla sin ser Manager.
/// </summary>
public sealed class WorkSchedulesState(IWorkSchedulesApi api, bool isManager)
{
    public const string NoPermissionMessage = "No tiene permiso para configurar jornadas.";
    public const string NotFoundMessage = "La jornada o la persona ya no existen. Recargue la página.";
    public const string TooManyRequestsMessage = "Demasiadas peticiones seguidas.";

    // Un campo type="time" entrega «HH:mm», pero Blazor lo recibe con segundos («HH:mm:ss») al enlazarlo.
    private static readonly string[] TimeFormats = ["HH:mm", "HH:mm:ss"];

    private static readonly string[] ShownFields = ["name", "startTime", "lunchStart", "lunchMinutes", "expectedBreakMinutes", "days"];

    private readonly FormFeedback feedback = new();

    private readonly HashSet<DayOfWeek> selectedDays = [];

    public IReadOnlyList<WorkScheduleDto> Items { get; private set; } = [];

    public IReadOnlyList<TeamMemberDto> Members { get; private set; } = [];

    /// <summary>Jornada en edición; <see langword="null"/> si el formulario es un alta.</summary>
    public Guid? EditingId { get; private set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Hora de entrada como «HH:mm» (el valor de un campo <c>type="time"</c>).</summary>
    public string StartTime { get; set; } = string.Empty;

    public string LunchStart { get; set; } = string.Empty;

    public int? LunchMinutes { get; set; }

    public int? ExpectedBreakMinutes { get; set; }

    public IReadOnlySet<DayOfWeek> SelectedDays => selectedDays;

    public bool IsBusy { get; private set; }

    /// <summary>Confirmación de la última operación correcta; excluye al error.</summary>
    public string? Notice { get; private set; }

    public string? ErrorMessage => feedback.Message;

    public IReadOnlyList<string> ErrorsFor(string field) => feedback.ErrorsFor(field);

    public IReadOnlyList<string> OtherErrors => feedback.OtherErrors(ShownFields);

    public void SetMembers(IReadOnlyList<TeamMemberDto> members)
    {
        ArgumentNullException.ThrowIfNull(members);

        Members = members;
    }

    public void SetDay(DayOfWeek day, bool selected)
    {
        if (selected)
        {
            selectedDays.Add(day);
        }
        else
        {
            selectedDays.Remove(day);
        }
    }

    /// <summary>Abre un alta limpia: vacía el formulario y descarta los mensajes anteriores.</summary>
    public void StartNew()
    {
        ClearMessages();
        NewSchedule();
    }

    public void NewSchedule()
    {
        EditingId = null;
        Name = string.Empty;
        StartTime = string.Empty;
        LunchStart = string.Empty;
        LunchMinutes = null;
        ExpectedBreakMinutes = null;
        selectedDays.Clear();
    }

    public void Edit(WorkScheduleDto schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        ClearMessages();
        EditingId = schedule.Id;
        Name = schedule.Name;
        StartTime = WorkSchedulePresentation.Time(schedule.StartTime);
        LunchStart = WorkSchedulePresentation.Time(schedule.LunchStart);
        LunchMinutes = schedule.LunchMinutes;
        ExpectedBreakMinutes = schedule.ExpectedBreakMinutes;
        selectedDays.Clear();
        selectedDays.UnionWith(schedule.Days);
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var result = await api.ListAsync(cancellationToken);
        if (result.Value is { } list)
        {
            Items = list.Items;
            return;
        }

        // El 401 lo trata el BearerTokenHandler (sesión cerrada y redirección a /login): aquí no se muestra.
        if (result.Error is { StatusCode: not HttpStatusCode.Unauthorized } failure)
        {
            Show(failure);
        }
    }

    public async Task SubmitAsync(CancellationToken cancellationToken = default)
    {
        if (!Begin())
        {
            return;
        }

        try
        {
            var request = new WorkScheduleRequestDto(
                Name.Trim(), ParseTime(StartTime), ParseTime(LunchStart), LunchMinutes, ExpectedBreakMinutes, [.. WorkSchedulePresentation.WeekOrder.Where(selectedDays.Contains)]);
            var result = EditingId is { } id
                ? await api.UpdateAsync(id, request, cancellationToken)
                : await api.CreateAsync(request, cancellationToken);

            if (result.Value is not null)
            {
                NewSchedule();
                Notice = "Jornada guardada.";
                await LoadAsync(cancellationToken);
            }
            else if (result.Error is { StatusCode: not HttpStatusCode.Unauthorized } failure)
            {
                Show(failure);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!Begin())
        {
            return;
        }

        try
        {
            var failure = await api.DeleteAsync(id, cancellationToken);
            if (failure is null)
            {
                if (EditingId == id)
                {
                    NewSchedule();
                }

                Notice = "Jornada borrada.";
                await LoadAsync(cancellationToken);
            }
            else if (failure.StatusCode is not HttpStatusCode.Unauthorized)
            {
                Show(failure);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Asigna la jornada a una persona del equipo, o la retira con <see langword="null"/>.</summary>
    public async Task AssignAsync(Guid memberId, Guid? workScheduleId, CancellationToken cancellationToken = default)
    {
        if (!Begin())
        {
            return;
        }

        try
        {
            var failure = await api.AssignAsync(memberId, workScheduleId, cancellationToken);
            if (failure is null)
            {
                Members = [.. Members.Select(member => member.Id == memberId ? member with { WorkScheduleId = workScheduleId } : member)];
                Notice = workScheduleId is null ? "Jornada retirada." : "Jornada asignada.";
                await LoadAsync(cancellationToken);
            }
            else if (failure.StatusCode is not HttpStatusCode.Unauthorized)
            {
                Show(failure);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    // Comprueba el permiso y limpia los mensajes; falso si no se debe llamar a la API.
    private bool Begin()
    {
        if (IsBusy)
        {
            return false;
        }

        ClearMessages();
        if (!isManager)
        {
            feedback.Show(NoPermissionMessage);
            return false;
        }

        IsBusy = true;
        return true;
    }

    private void ClearMessages()
    {
        Notice = null;
        feedback.Clear();
    }

    private void Show(ApiFailure failure)
        => feedback.ShowFailure(failure, TooManyRequestsMessage, candidate => candidate.StatusCode switch
        {
            HttpStatusCode.Forbidden => NoPermissionMessage,
            HttpStatusCode.NotFound => NotFoundMessage,
            HttpStatusCode.Conflict => candidate.Message,
            _ => null,
        });

    // Un valor vacío o ilegible viaja como nulo: la API responde con el error del campo.
    private static TimeOnly? ParseTime(string text)
        => TimeOnly.TryParseExact(text, TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ? time : null;
}
