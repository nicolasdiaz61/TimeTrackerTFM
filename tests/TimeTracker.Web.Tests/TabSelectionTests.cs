using TimeTracker.Web.Tabs;

namespace TimeTracker.Web.Tests;

public class TabSelectionTests
{
    private static readonly string[] Keys = ["personas", "fichajes"];

    [Theory]
    [InlineData("fichajes", "fichajes")]
    [InlineData("FICHAJES", "fichajes")]
    [InlineData("personas", "personas")]
    [InlineData("otra", "personas")]
    [InlineData("", "personas")]
    [InlineData(null, "personas")]
    public void Resolve_PicksTheRequestedTabOrTheFirst(string? requested, string expected)
        => Assert.Equal(expected, TabSelection.Resolve(requested, Keys));

    [Theory]
    [InlineData("personas", "ArrowRight", "fichajes")]
    [InlineData("fichajes", "ArrowRight", "personas")]
    [InlineData("personas", "ArrowLeft", "fichajes")]
    [InlineData("fichajes", "ArrowLeft", "personas")]
    [InlineData("personas", "Enter", "personas")]
    [InlineData("desconocida", "ArrowRight", "fichajes")]
    public void Move_UsesCircularArrows(string current, string key, string expected)
        => Assert.Equal(expected, TabSelection.Move(current, Keys, key));
}
