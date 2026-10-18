using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

using TimeTracker.Application.Identity;
using TimeTracker.Application.Team;
using TimeTracker.Application.Time;
using TimeTracker.Application.TimeTracking;
using TimeTracker.Domain.Users;

namespace TimeTracker.Infrastructure.Assistant;

/// <summary>
/// Herramientas de solo lectura del asistente (DD-7). La identidad sale siempre de <see cref="ICurrentUser"/>: ninguna
/// herramienta recibe un identificador de empleado, y la autorización real vive en los casos de uso.
/// </summary>
public sealed class AssistantToolset(
    GetTimeEntriesUseCase timeEntries,
    GetTeamMembersUseCase teamMembers,
    GetTeamMemberTimeEntriesUseCase teamMemberTimeEntries,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    ILogger<AssistantToolset> logger)
{
    private const string MyTimeEntriesTool = "get_my_time_entries";
    private const string TeamMembersTool = "get_team_members";
    private const string TeamMemberTimeEntriesTool = "get_team_member_time_entries";
    private const string FromDescription = "Primer día del periodo, formato yyyy-MM-dd (hora de España).";
    private const string ToDescription = "Último día del periodo, formato yyyy-MM-dd (hora de España).";
    private const string DateFormat = "yyyy-MM-dd";
    private const string InvalidDateMessage = "Las fechas deben tener el formato yyyy-MM-dd, por ejemplo 2026-10-18.";
    // Debe coincidir con LocalDateRange.MaxDays (un int no puede interpolarse en una constante); un test lo fija.
    internal const string MaxDaysText = "31";
    private const string DateRangeSuffix = $"entre dos fechas (máximo {MaxDaysText} días).";
    private const string MemberIdDescription = $"Identificador del miembro del equipo, tal como lo devuelve {TeamMembersTool}.";
    private const string UnavailableMessage = "No se ha podido completar la consulta en este momento.";

    /// <summary>Herramientas ofrecidas al modelo para el rol del usuario actual.</summary>
    public IReadOnlyList<AIFunction> CreateTools()
    {
        List<AIFunction> tools =
        [
            Create(
                GetMyTimeEntriesAsync,
                MyTimeEntriesTool,
                $"Devuelve un resumen por día, en hora de España, de los fichajes del propio usuario {DateRangeSuffix}"),
        ];

        // Defensa en profundidad (DD-7): un empleado no puede ni nombrar las herramientas de equipo.
        if (currentUser.Role == UserRole.Manager)
        {
            tools.Add(Create(
                GetTeamMembersAsync,
                TeamMembersTool,
                "Lista los miembros del equipo del usuario (sus reportes directos) con su identificador y nombre."));
            tools.Add(Create(
                GetTeamMemberTimeEntriesAsync,
                TeamMemberTimeEntriesTool,
                $"Devuelve un resumen por día, en hora de España, de los fichajes de un miembro del equipo {DateRangeSuffix}"));
        }

        return tools;
    }

    private static AIFunction Create(Delegate method, string name, string description)
        => AIFunctionFactory.Create(method, new AIFunctionFactoryOptions
        {
            Name = name,
            Description = description,
            SerializerOptions = AssistantJson.Options,
        });

    private Task<object> GetMyTimeEntriesAsync(
        [Description(FromDescription)] string fromDate,
        [Description(ToDescription)] string toDate,
        CancellationToken cancellationToken)
        => SummarizeAsync(
            MyTimeEntriesTool,
            fromDate,
            toDate,
            async range => await timeEntries.ExecuteAsync(range.ToWindow(), cancellationToken));

    private Task<object> GetTeamMembersAsync(CancellationToken cancellationToken)
        => RunAsync(TeamMembersTool, async () =>
        {
            var members = await teamMembers.ExecuteAsync(cancellationToken);
            return (ToolOutcome.Ok, (object)members.Select(member => new TeamMemberView(member.Id, DisplayNameSanitizer.Sanitize(member.DisplayName))).ToList());
        });

    private Task<object> GetTeamMemberTimeEntriesAsync(
        [Description(MemberIdDescription)] string memberId,
        [Description(FromDescription)] string fromDate,
        [Description(ToDescription)] string toDate,
        CancellationToken cancellationToken)
        => SummarizeAsync(
            TeamMemberTimeEntriesTool,
            fromDate,
            toDate,
            async range => Guid.TryParse(memberId, out var id)
                ? await teamMemberTimeEntries.ExecuteAsync(id, range.ToWindow(), cancellationToken)
                : null);

    // Las fechas se validan antes de consultar nada: un error de fechas no debe revelar si el miembro es del equipo (DD-8).
    // Una lista nula significa «no visible» (ajeno, inexistente, propio o ilegible) y siempre da la misma respuesta (DD-9).
    private Task<object> SummarizeAsync(string tool, string? fromDate, string? toDate, Func<LocalDateRange, Task<TimeEntryListResult?>> fetch)
        => RunAsync(tool, async () =>
        {
            if (!TryCreateRange(fromDate, toDate, out var range, out var error))
            {
                return (ToolOutcome.Invalid, ToolResults.Error(error));
            }

            var list = await fetch(range);
            return list is null
                ? (ToolOutcome.NotFound, (object)ToolResults.MemberNotFound())
                : (ToolOutcome.Ok, DailySummaryBuilder.Build(list, range, timeProvider.GetUtcNow()));
        });

    private bool TryCreateRange(string? fromDate, string? toDate, out LocalDateRange range, out string error)
    {
        range = null!;
        if (!DateOnly.TryParseExact(fromDate, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var from)
            || !DateOnly.TryParseExact(toDate, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var to))
        {
            error = InvalidDateMessage;
            return false;
        }

        var today = SpainTime.DateOf(timeProvider.GetUtcNow());
        if (LocalDateRange.TryCreate(from, to, today, out var created, out var message))
        {
            range = created;
            error = string.Empty;
            return true;
        }

        error = message;
        return false;
    }

    // Ejecuta una herramienta: mide, registra solo metadatos y convierte un fallo inesperado en un mensaje genérico
    // (sin texto ni tipo de la excepción) para que el modelo no vea ni repita detalles internos.
    private async Task<object> RunAsync(string tool, Func<Task<(string Outcome, object Result)>> run)
    {
        var started = Stopwatch.GetTimestamp();
        var outcome = ToolOutcome.Error;
        try
        {
            (outcome, var result) = await run();
            return result;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return ToolResults.Error(UnavailableMessage);
        }
        finally
        {
            AssistantLog.ToolExecuted(logger, tool, currentUser.EmployeeId, outcome, (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }
}

/// <summary>Mínima exposición al modelo: identificador y nombre saneado; sin correo, hash, rol ni responsable.</summary>
internal sealed record TeamMemberView(Guid Id, string DisplayName);
