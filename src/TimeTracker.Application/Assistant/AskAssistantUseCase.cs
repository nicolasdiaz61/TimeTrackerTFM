using TimeTracker.Application.Identity;

namespace TimeTracker.Application.Assistant;

public sealed class AskAssistantUseCase(IAssistantEngine engine, IAssistantQuota quota, ICurrentUser currentUser)
{
    public const int MaxMessageLength = 500;
    public const int MaxHistoryTurns = 10;
    public const int MaxTurnLength = 2000;
    public const int MaxHistoryLength = 8000;

    private static readonly string InvalidMessage = $"El mensaje debe tener entre 1 y {MaxMessageLength} caracteres.";
    private static readonly string TooManyTurns = $"El historial admite como máximo {MaxHistoryTurns} mensajes.";
    private static readonly string InvalidTurnText = $"Cada mensaje del historial debe tener entre 1 y {MaxTurnLength} caracteres.";
    private static readonly string HistoryTooLong = $"El historial no puede superar los {MaxHistoryLength} caracteres en total.";
    private const string InvalidRole = "El historial solo admite mensajes de los roles user y assistant.";

    /// <summary>Cuenta el minuto de la compañía, valida, comprueba que el asistente está habilitado, consume el tope diario y consulta al motor; en ese orden.</summary>
    public async Task<AskAssistantResult> ExecuteAsync(AskAssistantCommand command, CancellationToken cancellationToken)
    {
        var minute = quota.TryConsumeMinute(currentUser.CompanyId);
        if (!minute.Allowed)
        {
            return new AskAssistantResult.QuotaExceeded(minute.RetryAfter, minute.Scope);
        }

        var message = command.Message?.Trim();
        if (string.IsNullOrEmpty(message) || message.Length > MaxMessageLength)
        {
            return new AskAssistantResult.Rejected(InvalidMessage);
        }

        if (!TryReadHistory(command.History ?? [], out var history, out var error))
        {
            return new AskAssistantResult.Rejected(error);
        }

        if (!engine.IsEnabled)
        {
            return new AskAssistantResult.Unavailable(Disabled: true);
        }

        var decision = quota.TryConsumeDaily(currentUser.CompanyId, currentUser.EmployeeId);
        if (!decision.Allowed)
        {
            return new AskAssistantResult.QuotaExceeded(decision.RetryAfter, decision.Scope);
        }

        var reply = await engine.ReplyAsync(new AssistantConversation(history, message), cancellationToken);
        return reply is null
            ? new AskAssistantResult.Unavailable(Disabled: false)
            : new AskAssistantResult.Answered(reply);
    }

    private static bool TryReadHistory(
        IReadOnlyList<AssistantTurnInput> input,
        out IReadOnlyList<ConversationTurn> history,
        out string error)
    {
        history = [];
        error = string.Empty;
        if (input.Count > MaxHistoryTurns)
        {
            error = TooManyTurns;
            return false;
        }

        var turns = new List<ConversationTurn>(input.Count);
        var total = 0;
        foreach (var turn in input)
        {
            var role = turn?.Role switch
            {
                "user" => ConversationRole.User,
                "assistant" => ConversationRole.Assistant,
                _ => (ConversationRole?)null,
            };
            if (role is null)
            {
                error = InvalidRole;
                return false;
            }

            if (string.IsNullOrWhiteSpace(turn!.Text) || turn.Text.Length > MaxTurnLength)
            {
                error = InvalidTurnText;
                return false;
            }

            total += turn.Text.Length;
            turns.Add(new ConversationTurn(role.Value, turn.Text));
        }

        if (total > MaxHistoryLength)
        {
            error = HistoryTooLong;
            return false;
        }

        history = turns;
        return true;
    }
}
