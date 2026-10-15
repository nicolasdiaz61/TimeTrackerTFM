using TimeTracker.Web.Formatting;

namespace TimeTracker.Web.Tests;

public class TimeEntryPresentationTests
{
    [Theory]
    [InlineData("Work", "Trabajo")]
    [InlineData("Break", "Pausa")]
    [InlineData("MealBreak", "Comida")]
    [InlineData("Otro", "Otro")]
    public void TypeLabel_IsInSpanishAndKeepsUnknownValues(string type, string expected)
        => Assert.Equal(expected, TimeEntryPresentation.TypeLabel(type));

    [Theory]
    [InlineData("Idle", "Sin fichar")]
    [InlineData("Working", "Trabajando")]
    [InlineData("OnBreak", "En pausa")]
    [InlineData("OnMealBreak", "En pausa de comida")]
    [InlineData("Otro", "Otro")]
    public void StatusLabel_IsInSpanishAndKeepsUnknownValues(string status, string expected)
        => Assert.Equal(expected, TimeEntryPresentation.StatusLabel(status));

    [Theory]
    [InlineData("Idle", new[] { ClockAction.ClockIn })]
    [InlineData("Working", new[] { ClockAction.StartBreak, ClockAction.StartMealBreak, ClockAction.ClockOut })]
    [InlineData("OnBreak", new[] { ClockAction.EndBreak, ClockAction.ClockOut })]
    [InlineData("OnMealBreak", new[] { ClockAction.EndBreak, ClockAction.ClockOut })]
    public void AvailableActions_ShowsOnlyTheValidOnesForTheStatus(string status, ClockAction[] expected)
        => Assert.Equal(expected, TimeEntryPresentation.AvailableActions(status));

    [Fact]
    public void AvailableActions_ForAnUnknownStatus_FallsBackToEntryAndExit()
        => Assert.Equal([ClockAction.ClockIn, ClockAction.ClockOut], TimeEntryPresentation.AvailableActions("Otro"));

    [Theory]
    [InlineData(ClockAction.ClockIn, "Iniciar jornada")]
    [InlineData(ClockAction.StartBreak, "Pausa")]
    [InlineData(ClockAction.StartMealBreak, "Comida")]
    [InlineData(ClockAction.EndBreak, "Reanudar")]
    [InlineData(ClockAction.ClockOut, "Finalizar jornada")]
    public void ActionLabel_IsInSpanish(ClockAction action, string expected)
        => Assert.Equal(expected, TimeEntryPresentation.ActionLabel(action));

    [Theory]
    [InlineData(ClockAction.ClockIn, "Iniciar la jornada")]
    [InlineData(ClockAction.StartBreak, "Pausar la jornada")]
    [InlineData(ClockAction.StartMealBreak, "Pausar la jornada para comer")]
    [InlineData(ClockAction.EndBreak, "Reanudar el trabajo")]
    [InlineData(ClockAction.ClockOut, "Finalizar la jornada")]
    public void ActionAriaLabel_DescribesWhatTheButtonDoes(ClockAction action, string expected)
        => Assert.Equal(expected, TimeEntryPresentation.ActionAriaLabel(action));

    [Theory]
    [InlineData(ClockAction.ClockIn, "btn-primary")]
    [InlineData(ClockAction.StartBreak, "btn-warning")]
    [InlineData(ClockAction.StartMealBreak, "btn-info")]
    [InlineData(ClockAction.EndBreak, "btn-success")]
    [InlineData(ClockAction.ClockOut, "btn-danger")]
    public void ActionButtonClass_GivesEachActionItsOwnColour(ClockAction action, string expected)
        => Assert.Equal(expected, TimeEntryPresentation.ActionButtonClass(action));

    [Theory]
    [InlineData("Idle", "status-idle")]
    [InlineData("Working", "status-working")]
    [InlineData("OnBreak", "status-break")]
    [InlineData("OnMealBreak", "status-meal")]
    [InlineData("Otro", "status-idle")]
    public void StatusCssClass_MatchesTheStatus(string status, string expected)
        => Assert.Equal(expected, TimeEntryPresentation.StatusCssClass(status));
}
