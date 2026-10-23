using TimeTracker.Application.Assistant.Configuration;

namespace TimeTracker.Application.WorkSchedules;

public abstract record SaveWorkScheduleResult
{
    private SaveWorkScheduleResult()
    {
    }

    public sealed record Saved(WorkScheduleView View) : SaveWorkScheduleResult;

    public sealed record Invalid(IReadOnlyList<FieldError> Errors) : SaveWorkScheduleResult;

    public sealed record DuplicateName : SaveWorkScheduleResult;

    public sealed record CapReached : SaveWorkScheduleResult;

    /// <summary>Inexistente o de otra compañía: indistinguibles.</summary>
    public sealed record NotFound : SaveWorkScheduleResult;
}
