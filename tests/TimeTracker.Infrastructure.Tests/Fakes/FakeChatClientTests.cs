using Microsoft.Extensions.AI;

using TimeTracker.Infrastructure.Tests.Fakes;

namespace TimeTracker.Infrastructure.Tests.Fakes;

public class FakeChatClientTests
{
    [Fact]
    public async Task GetResponseAsync_ReturnsScriptedTurnsInOrder_AndRecordsMessagesAndOptions()
    {
        var client = new FakeChatClient(
            _ => new ChatResponse(new ChatMessage(ChatRole.Assistant, "uno")),
            _ => new ChatResponse(new ChatMessage(ChatRole.Assistant, "dos")));
        var options = new ChatOptions { MaxOutputTokens = 300 };

        var first = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hola")], options);
        var second = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "otra")], options);

        Assert.Equal("uno", first.Text);
        Assert.Equal("dos", second.Text);
        Assert.Equal(2, client.Calls.Count);
        Assert.Equal("hola", client.Calls[0].Messages.Single().Text);
        Assert.Equal(300, client.Calls[1].Options?.MaxOutputTokens);
    }

    [Fact]
    public async Task GetResponseAsync_WithoutScriptedTurnLeft_Throws()
    {
        var client = new FakeChatClient();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetResponseAsync([new ChatMessage(ChatRole.User, "hola")]));
    }
}
