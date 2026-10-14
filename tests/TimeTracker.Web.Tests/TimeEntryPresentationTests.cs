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
    [InlineData(ClockAction.ClockIn, "Fichar entrada")]
    [InlineData(ClockAction.StartBreak, "Iniciar pausa")]
    [InlineData(ClockAction.StartMealBreak, "Pausa de comida")]
    [InlineData(ClockAction.EndBreak, "Terminar pausa")]
    [InlineData(ClockAction.ClockOut, "Fichar salida")]
    public void ActionLabel_IsInSpanish(ClockAction action, string expected)
        => Assert.Equal(expected, TimeEntryPresentation.ActionLabel(action));
}
