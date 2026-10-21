using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

using TimeTracker.Application.Assistant;
using TimeTracker.Application.Identity;
using TimeTracker.Application.Time;
using TimeTracker.Domain.Companies;

namespace TimeTracker.Infrastructure.Assistant;

/// <summary>
/// Motor del asistente (DD-11, DD-12): monta los mensajes, ofrece las herramientas del rol del token y acota el tiempo.
/// Es de ámbito de petición porque <see cref="AssistantToolset"/> depende de la identidad y del <c>DbContext</c> del ámbito.
/// </summary>
public sealed class ChatAssistantEngine(
    AssistantChatClient client,
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

    // Puente transitorio (PR 1b a 4b, DD-14): el cliente singleton lleva la instantánea de la compañía de plataforma (la por defecto,
    // con uses_platform_assistant). Cualquier otra compañía recibe el asistente deshabilitado: nunca su instantánea ni su clave,
    // tenga o no fila propia. La 4b sustituye el singleton por el registro de instantáneas por compañía y retira esta guarda.
    private bool ServesCompany => currentUser.CompanyId == Company.DefaultId;

    public bool IsEnabled => ServesCompany && client.IsEnabled;

    public async Task<string?> ReplyAsync(AssistantConversation conversation, CancellationToken cancellationToken)
    {
        if (!ServesCompany)
        {
            return null;
        }

        // Una sola lectura: un cambio de configuración a mitad de la petición no la afecta (DD-10).
        var snapshot = client.Current;
        if (snapshot.Pipeline is not { } pipeline)
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
