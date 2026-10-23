namespace TimeTracker.Web.WorkSchedules;

public enum WorkScheduleDialog
{
    None,
    Form,
    ConfirmDelete,
}

/// <summary>
/// Qué diálogo muestra /jornadas: el formulario (alta o edición, el mismo componente) o la confirmación de borrado. Reutiliza
/// <see cref="WorkSchedulesState"/> para toda la lógica; aquí solo se decide cuándo abrir y cerrar.
/// </summary>
public sealed class WorkScheduleDialogs(WorkSchedulesState state)
{
    public WorkScheduleDialog Current { get; private set; }

    /// <summary>Jornada pendiente de confirmar su borrado.</summary>
    public WorkScheduleDto? PendingDelete { get; private set; }

    public bool IsEditing => Current == WorkScheduleDialog.Form && state.EditingId is not null;

    public string FormTitle => state.EditingId is null ? "Nueva jornada" : "Editar jornada";

    public void OpenCreate()
    {
        state.StartNew();
        Current = WorkScheduleDialog.Form;
    }

    public void OpenEdit(WorkScheduleDto schedule)
    {
        state.Edit(schedule);
        Current = WorkScheduleDialog.Form;
    }

    public void RequestDelete(WorkScheduleDto schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        PendingDelete = schedule;
        Current = WorkScheduleDialog.ConfirmDelete;
    }

    /// <summary>Cierra sin guardar: el formulario vuelve a quedar vacío.</summary>
    public void Close()
    {
        if (Current == WorkScheduleDialog.Form)
        {
            state.NewSchedule();
        }

        Current = WorkScheduleDialog.None;
        PendingDelete = null;
    }

    /// <summary>Guarda; el diálogo se cierra solo si la operación terminó bien (si no, queda abierto con los errores).</summary>
    public async Task SubmitAsync(CancellationToken cancellationToken = default)
    {
        await state.SubmitAsync(cancellationToken);
        if (state.Notice is not null)
        {
            Current = WorkScheduleDialog.None;
        }
    }

    /// <summary>Borra la jornada pendiente y cierra; si falla, el error se ve en la página.</summary>
    public async Task ConfirmDeleteAsync(CancellationToken cancellationToken = default)
    {
        if (PendingDelete is { } schedule)
        {
            await state.DeleteAsync(schedule.Id, cancellationToken);
        }

        Close();
    }
}
