using System.Text.Json;

using TimeTracker.Infrastructure.Assistant;

namespace TimeTracker.Infrastructure.Tests.Assistant;

public class ToolResultsTests
{
    [Fact]
    public void Error_SerializesAsObjectWithOnlyAnErrorProperty()
    {
        var json = JsonSerializer.Serialize(ToolResults.Error("Fecha no válida."), AssistantJson.Options);

        Assert.Equal("""{"error":"Fecha no válida."}""", json);
    }

    [Fact]
    public void MemberNotFound_IsTheSingleFixedMessage()
    {
        Assert.Equal("No hay ningún miembro de tu equipo con ese identificador.", ToolResults.MemberNotFoundMessage);
    }
}
