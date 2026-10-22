using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

using TimeTracker.Application.Assistant;
using TimeTracker.Application.Identity;
using TimeTracker.Application.Time;

namespace TimeTracker.Infrastructure.Assistant;

/// <summary>
/// Motor del asistente (DD-11, DD-12): monta los mensajes, ofrece las herramientas del rol del token y acota el tiempo.
/// Es de ámbito de petición porque <see cref="AssistantToolset"/> depende de la identidad y del <c>DbContext</c> del ámbito.
/// </summary>
internal sealed class ChatAssistantEngine(
    IAssistantRuntimeRegistry registry,
    AssistantToolset toolset,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    ILogger<ChatAssistantEngine> logger) : IAssistantEngine
{
    internal const string FallbackReply = "No he podido completar la consulta. Pruebe a reformularla.";

    private const string OkOutcome = "ok";
    private const string EmptyOutcome = "empty";
    private const string ErrorOutcome = "error";
    private const string TimeoutOutcome = "timeout";
    private const string ToolErrorOutcome = "tool-error";

    // Una sola lectura de la instantánea de la compañía por petición (el motor es de ámbito de petición): un Swap a mitad de la
    // petición no la afecta (DD-10) y la compañía sale siempre del token, nunca de un parámetro.
    private AssistantSnapshot? _snapshot;

    public async Task<AssistantAvailability> GetAvailabilityAsync(CancellationToken cancellationToken)
    {
        if (await SnapshotAsync(cancellationToken) is not { } snapshot)
        {
            return AssistantAvailability.Unavailable;
        }

        return snapshot.Pipeline is null ? AssistantAvailability.Disabled : AssistantAvailability.Enabled;
    }

    public async Task<string?> ReplyAsync(AssistantConversation conversation, CancellationToken cancellationToken)
    {
        var snapshot = await SnapshotAsync(cancellationToken);
        if (snapshot?.Pipeline is not { } pipeline)
        {
            return null;
        }

        var settings = snapshot.Options;
        var provider = settings.ResolvedProvider?.ToString();
        var started = timeProvider.GetTimestamp();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(settings.TimeoutSeconds), timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        try
        {
            var chatOptions = AssistantChatPipeline.CreateChatOptions(settings, toolset.CreateTools());
            var response = await pipeline.GetResponseAsync(BuildMessages(conversation), chatOptions, linked.Token);

            var reply = response.Text;
            var outcome = string.IsNullOrWhiteSpace(reply) ? EmptyOutcome : OkOutcome;
            var toolCalls = response.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>().Count();
            AssistantLog.QueryCompleted(
                logger, currentUser.EmployeeId, provider, settings.Model, outcome, ElapsedMs(started),
                response.Usage?.InputTokenCount, response.Usage?.OutputTokenCount, toolCalls);

            return outcome == OkOutcome ? reply : FallbackReply;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // El cliente abortó: no es un fallo del proveedor y no hay a quién responder.
            throw;
        }
        catch (AssistantToolFailureException exception)
        {
            // El bucle se cortó por errores consecutivos de herramienta (argumentos que el modelo no acierta a dar):
            // no es un fallo del proveedor, así que se responde con el texto fijo y no con un 503.
            AssistantLog.QueryFailed(logger, currentUser.EmployeeId, provider, settings.Model, ToolErrorOutcome, ElapsedMs(started), exception.InnerException!.GetType().Name);
            return FallbackReply;
        }
        catch (Exception exception)
        {
            // Tiempo agotado, fallo del proveedor o de la herramienta (incluidos argumentos ausentes o mal tipados):
            // un único «no disponible», sin el mensaje de la excepción, que puede traer datos o la clave.
            var outcome = timeout.IsCancellationRequested ? TimeoutOutcome : ErrorOutcome;
            AssistantLog.QueryFailed(logger, currentUser.EmployeeId, provider, settings.Model, outcome, ElapsedMs(started), exception.GetType().Name);
            return null;
        }
    }

    // Un fallo de infraestructura al cargar la compañía (base de datos caída o plazo agotado) no se oculta como «deshabilitado»: es
    // «no disponible», sin cachear, y la siguiente petición lo reintenta (DD-14). El mensaje de la excepción no se registra.
    private async Task<AssistantSnapshot?> SnapshotAsync(CancellationToken cancellationToken)
    {
        if (_snapshot is not null)
        {
            return _snapshot;
        }

        try
        {
            return _snapshot = await registry.GetAsync(currentUser.CompanyId, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            AssistantLog.RuntimeLoadFailed(logger, exception.GetType().Name);
            return null;
        }
    }

    private List<ChatMessage> BuildMessages(AssistantConversation conversation)
    {
        var today = SpainTime.DateOf(timeProvider.GetUtcNow());
        List<ChatMessage> messages = [new(ChatRole.System, AssistantPrompt.Build(today, currentUser.Role))];
        messages.AddRange(conversation.History.Select(turn => new ChatMessage(ToChatRole(turn.Role), turn.Text)));
        messages.Add(new ChatMessage(ChatRole.User, conversation.Message));
        return messages;
    }

    private static ChatRole ToChatRole(ConversationRole role)
        => role == ConversationRole.Assistant ? ChatRole.Assistant : ChatRole.User;

    private long ElapsedMs(long started) => (long)timeProvider.GetElapsedTime(started).TotalMilliseconds;
}
