using Automation.Flow;

namespace Automation.Tests;

public class CodeFeedTests
{
    private static readonly DateTimeOffset RequestedAt = DateTimeOffset.FromUnixTimeMilliseconds(1789794000000);

    private static string Line(long internalDate, string subject)
    {
        return $"{{\"internalDate\": {internalDate}, \"subject\": \"{subject}\"}}";
    }

    [Fact]
    public void NewestMatchingSubjectWins()
    {
        string[] feed =
        [
            Line(1789795006000, "Fw: Your EA Security Code is: 389839"),
            Line(1789794116000, "Fw: Your EA Security Code is: 111111")
        ];

        Assert.Equal("389839", VerificationCode.FromFeed(feed, RequestedAt));
    }

    [Fact]
    public void MessagesOlderThanTheRequestAreIgnored()
    {
        string[] feed = [Line(1789793000000, "Fw: Your EA Security Code is: 389839")];

        Assert.Equal(string.Empty, VerificationCode.FromFeed(feed, RequestedAt));
    }

    [Fact]
    public void SubjectsWithoutACodeAreSkipped()
    {
        string[] feed =
        [
            Line(1789795006000, "Fw: Security alert for someone"),
            Line(1789794500000, "Fw: Your EA Security Code is: 222222")
        ];

        Assert.Equal("222222", VerificationCode.FromFeed(feed, RequestedAt));
    }

    [Fact]
    public void AnEmptyOrMalformedFeedYieldsNothing()
    {
        Assert.Equal(string.Empty, VerificationCode.FromFeed([], RequestedAt));
        Assert.Equal(string.Empty, VerificationCode.FromFeed(["", "not json"], RequestedAt));
    }
}
