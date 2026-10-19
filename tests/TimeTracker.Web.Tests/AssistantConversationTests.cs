using System.Net;

using TimeTracker.Web.Assistant;
using TimeTracker.Web.Http;

namespace TimeTracker.Web.Tests;

public class AssistantConversationTests
{
    private const string UnavailableDetail = "El asistente no está disponible en este entorno.";

    private static AssistantRequestDto Begin(AssistantConversation conversation, string message)
        => conversation.TryBegin(message) ?? throw new InvalidOperationException(conversation.ErrorMessage);

    private static void Exchange(AssistantConversation conversation, string message, string reply)
    {
        Begin(conversation, message);
        conversation.Complete(reply);
    }

    [Fact]
    public void FirstMessage_SendsEmptyHistoryAndShowsTheUserTurn()
    {
        var conversation = new AssistantConversation();

        var request = Begin(conversation, "  ¿Cuántas horas trabajé ayer?  ");

        Assert.Equal("¿Cuántas horas trabajé ayer?", request.Message);
        Assert.Empty(request.History);
        Assert.Equal([new ConversationTurn(TurnRole.User, "¿Cuántas horas trabajé ayer?")], conversation.Turns);
        Assert.True(conversation.IsBusy);
    }

    [Fact]
    public void Complete_AddsTheReplyAndReleasesTheBusyState()
    {
        var conversation = new AssistantConversation();
        Begin(conversation, "Hola");

        conversation.Complete("Trabajó 8 horas.");

        Assert.False(conversation.IsBusy);
        Assert.Equal([new ConversationTurn(TurnRole.User, "Hola"), new ConversationTurn(TurnRole.Assistant, "Trabajó 8 horas.")], conversation.Turns);
    }

    [Fact]
    public void SecondMessage_SendsOnlyUserAndAssistantTurnsInOrder()
    {
        var conversation = new AssistantConversation();
        Exchange(conversation, "Uno", "Respuesta uno");

        var request = Begin(conversation, "Dos");

        Assert.Equal([new AssistantTurnDto("user", "Uno"), new AssistantTurnDto("assistant", "Respuesta uno")], request.History);
    }

    [Fact]
    public void History_KeepsAtMostTheTenMostRecentTurns()
    {
        var conversation = new AssistantConversation();
        for (var index = 1; index <= 8; index++)
        {
            Exchange(conversation, $"p{index}", $"r{index}");
        }

        var request = Begin(conversation, "último");

        Assert.Equal(10, request.History.Count);
        Assert.Equal("p4", request.History[0].Text);
        Assert.Equal("r8", request.History[^1].Text);
    }

    [Fact]
    public void History_KeepsAtMostEightThousandCharactersWithTheMostRecentTurns()
    {
        var conversation = new AssistantConversation();
        Exchange(conversation, new string('a', 400), new string('b', 1900));
        Exchange(conversation, new string('c', 400), new string('d', 1900));
        Exchange(conversation, new string('e', 400), new string('f', 1900));
        Exchange(conversation, new string('g', 400), new string('h', 1900));

        var request = Begin(conversation, "siguiente");

        Assert.True(request.History.Sum(turn => turn.Text.Length) <= 8000);
        Assert.Equal(new string('h', 1900), request.History[^1].Text);
        Assert.DoesNotContain(request.History, turn => turn.Text.StartsWith('a'));
        Assert.Equal(6, request.History.Count);
    }

    [Fact]
    public void History_TruncatesATurnLongerThanTheServerLimit()
    {
        var conversation = new AssistantConversation();
        Exchange(conversation, "Hola", new string('r', 2500));

        var request = Begin(conversation, "Otra");

        Assert.Equal(2000, request.History[^1].Text.Length);
        Assert.Equal(2500, conversation.Turns[1].Text.Length);
    }

    [Fact]
    public void Screen_KeepsAtMostFiveHundredTurns()
    {
        var conversation = new AssistantConversation();
        for (var index = 1; index <= 260; index++)
        {
            Exchange(conversation, $"p{index}", $"r{index}");
        }

        Assert.Equal(500, conversation.Turns.Count);
        Assert.Equal("p11", conversation.Turns[0].Text);
        Assert.Equal("r260", conversation.Turns[^1].Text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n\t ")]
    public void EmptyMessage_IsRejectedWithoutBeingSent(string? message)
    {
        var conversation = new AssistantConversation();

        Assert.Null(conversation.TryBegin(message));
        Assert.Equal("Escriba un mensaje para el asistente.", conversation.ErrorMessage);
        Assert.False(conversation.IsBusy);
        Assert.Empty(conversation.Turns);
    }

    [Fact]
    public void LongMessage_IsRejectedAt501ButAcceptedAt500()
    {
        var conversation = new AssistantConversation();

        Assert.Null(conversation.TryBegin(new string('x', 501)));
        Assert.Equal("El mensaje no puede superar los 500 caracteres.", conversation.ErrorMessage);
        Assert.Empty(conversation.Turns);

        Assert.NotNull(conversation.TryBegin(new string('x', 500)));
        Assert.Null(conversation.ErrorMessage);
    }

    [Fact]
    public void Validate_ReturnsTheSpanishReasonOrNull()
    {
        var conversation = new AssistantConversation();

        Assert.NotNull(conversation.Validate(" "));
        Assert.NotNull(conversation.Validate(new string('x', 501)));
        Assert.Null(conversation.Validate("Hola"));
    }

    [Fact]
    public void WhileBusy_ASecondSendIsRefusedWithoutAddingTurns()
    {
        var conversation = new AssistantConversation();
        Begin(conversation, "Primero");

        Assert.Null(conversation.TryBegin("Segundo"));
        Assert.Single(conversation.Turns);
        Assert.True(conversation.IsBusy);
    }

    [Fact]
    public void Fail_RemovesThePendingTurnAndKeepsItOutOfTheNextHistory()
    {
        var conversation = new AssistantConversation();
        Exchange(conversation, "Uno", "Respuesta");
        Begin(conversation, "Fallido");

        conversation.Fail(new ApiRequestException(ApiRequest.GenericErrorMessage));
        var next = Begin(conversation, "Reintento");

        Assert.DoesNotContain(conversation.Turns, turn => turn.Text == "Fallido");
        Assert.DoesNotContain(next.History, turn => turn.Text == "Fallido");
        Assert.Equal(2, next.History.Count);
    }

    [Fact]
    public void Fail_ReleasesTheBusyState()
    {
        var conversation = new AssistantConversation();
        Begin(conversation, "Hola");

        conversation.Fail(new ApiRequestException(ApiRequest.GenericErrorMessage));

        Assert.False(conversation.IsBusy);
        Assert.Empty(conversation.Turns);
    }

    [Fact]
    public void BadRequest_ShowsTheApiDetail_OrAFallback()
    {
        var conversation = new AssistantConversation();
        Begin(conversation, "Hola");
        conversation.Fail(new ApiRequestException("El mensaje debe tener entre 1 y 500 caracteres.", HttpStatusCode.BadRequest));
        Assert.Equal("El mensaje debe tener entre 1 y 500 caracteres.", conversation.ErrorMessage);

        Begin(conversation, "Hola");
        conversation.Fail(new ApiRequestException(ApiRequest.GenericErrorMessage, HttpStatusCode.BadRequest));
        Assert.Equal("El mensaje no es válido. Revise el texto e inténtelo de nuevo.", conversation.ErrorMessage);
    }

    [Fact]
    public void TooManyRequests_WithRetryAfter_AddsTheWait()
    {
        var conversation = new AssistantConversation();
        Begin(conversation, "Hola");

        conversation.Fail(new ApiRequestException("Ha superado el límite de consultas por minuto.", HttpStatusCode.TooManyRequests), retryAfterSeconds: 30);

        Assert.Equal("Ha superado el límite de consultas por minuto. Espere unos 30 segundos antes de reintentar.", conversation.ErrorMessage);
    }

    [Fact]
    public void TooManyRequests_WithOneSecond_UsesTheSingular()
    {
        var conversation = new AssistantConversation();
        Begin(conversation, "Hola");

        conversation.Fail(new ApiRequestException("Límite.", HttpStatusCode.TooManyRequests), retryAfterSeconds: 1);

        Assert.Equal("Límite. Espere 1 segundo antes de reintentar.", conversation.ErrorMessage);
    }

    [Fact]
    public void TooManyRequests_WithoutRetryAfter_ShowsOnlyTheMessage()
    {
        var conversation = new AssistantConversation();
        Begin(conversation, "Hola");
        conversation.Fail(new ApiRequestException("Ha superado el límite de consultas por minuto.", HttpStatusCode.TooManyRequests));
        Assert.Equal("Ha superado el límite de consultas por minuto.", conversation.ErrorMessage);

        Begin(conversation, "Hola");
        conversation.Fail(new ApiRequestException(ApiRequest.GenericErrorMessage, HttpStatusCode.TooManyRequests));
        Assert.Equal("Demasiadas consultas al asistente. Inténtelo de nuevo en unos minutos.", conversation.ErrorMessage);
    }

    [Fact]
    public void ServiceUnavailable_SetsAPersistentNotice_AndNoError()
    {
        var conversation = new AssistantConversation();
        Begin(conversation, "Hola");

        conversation.Fail(new ApiRequestException(UnavailableDetail, HttpStatusCode.ServiceUnavailable));

        Assert.Equal(UnavailableDetail, conversation.Notice);
        Assert.Null(conversation.ErrorMessage);
        Assert.Empty(conversation.Turns);
    }

    [Fact]
    public void ServiceUnavailable_WithoutDetail_UsesTheWebMessage()
    {
        var conversation = new AssistantConversation();
        Begin(conversation, "Hola");

        conversation.Fail(new ApiRequestException(ApiRequest.GenericErrorMessage, HttpStatusCode.ServiceUnavailable));

        Assert.Equal("El asistente no está disponible en este momento. Inténtelo de nuevo más tarde.", conversation.Notice);
    }

    [Fact]
    public void Notice_PersistsAcrossAnotherSendAndClearsAfterASuccessfulReply()
    {
        var conversation = new AssistantConversation();
        Begin(conversation, "Hola");
        conversation.Fail(new ApiRequestException(UnavailableDetail, HttpStatusCode.ServiceUnavailable));

        Begin(conversation, "Otra vez");
        Assert.Equal(UnavailableDetail, conversation.Notice);

        conversation.Complete("Respuesta");
        Assert.Null(conversation.Notice);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(null)]
    public void OtherFailures_ShowTheGenericMessageEvenIfTheExceptionCarriesAnotherText(HttpStatusCode? status)
    {
        var conversation = new AssistantConversation();
        Begin(conversation, "Hola");

        conversation.Fail(new ApiRequestException("Traza interna con proveedor y clave.", status));

        Assert.Equal(ApiRequest.GenericErrorMessage, conversation.ErrorMessage);
        Assert.Null(conversation.Notice);
    }

    [Fact]
    public void NextSend_ClearsThePreviousError()
    {
        var conversation = new AssistantConversation();
        Begin(conversation, "Hola");
        conversation.Fail(new ApiRequestException(ApiRequest.GenericErrorMessage));
        Assert.NotNull(conversation.ErrorMessage);

        Begin(conversation, "Hola");

        Assert.Null(conversation.ErrorMessage);
    }
}
