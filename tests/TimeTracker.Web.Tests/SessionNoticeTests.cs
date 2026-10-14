using TimeTracker.Web.Auth;

namespace TimeTracker.Web.Tests;

public class SessionNoticeTests
{
    [Fact]
    public void Consume_WithoutMark_ReturnsFalse()
        => Assert.False(new SessionNotice().Consume());

    [Fact]
    public void Consume_AfterMarkExpired_ReturnsTrueOnlyOnce()
    {
        var notice = new SessionNotice();
        notice.MarkExpired();

        Assert.True(notice.Consume());
        Assert.False(notice.Consume());
    }

    [Fact]
    public void Clear_DiscardsPendingNotice()
    {
        var notice = new SessionNotice();
        notice.MarkExpired();

        notice.Clear();

        Assert.False(notice.Consume());
    }
}
