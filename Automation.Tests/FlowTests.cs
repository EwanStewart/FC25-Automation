using Automation.Flow;

namespace Automation.Tests;

public class FlowTests
{
    [Fact]
    public void NextStepIsDoneWhenNavigationIsVisible()
    {
        LoginScreen screen = new(true, true, true, true, true, false);

        Assert.Equal(LoginStep.Done, LoginFlow.NextStep(screen));
    }

    [Fact]
    public void NextStepFailsOnUnsupportedBrowser()
    {
        LoginScreen screen = new(false, false, false, false, true, true);

        Assert.Equal(LoginStep.Unsupported, LoginFlow.NextStep(screen));
    }

    [Fact]
    public void NextStepPrefersContinueThenPasswordThenEmailThenLogin()
    {
        Assert.Equal(LoginStep.Continue, LoginFlow.NextStep(new LoginScreen(false, true, true, true, true, false)));
        Assert.Equal(LoginStep.EnterPassword,
            LoginFlow.NextStep(new LoginScreen(false, false, true, true, true, false)));
        Assert.Equal(LoginStep.EnterEmail, LoginFlow.NextStep(new LoginScreen(false, false, false, true, true, false)));
        Assert.Equal(LoginStep.ClickLogin,
            LoginFlow.NextStep(new LoginScreen(false, false, false, false, true, false)));
    }

    [Fact]
    public void NextStepWaitsWhenNothingRecognisableIsShown()
    {
        Assert.Equal(LoginStep.Wait, LoginFlow.NextStep(new LoginScreen(false, false, false, false, false, false)));
    }

    [Theory]
    [InlineData("listFUTItem has-auction-data selected highest-bid", true)]
    [InlineData("listFUTItem has-auction-data selected outbid", true)]
    [InlineData("listFUTItem has-auction-data selected", false)]
    [InlineData("", false)]
    public void IsRegisteredReadsBidStateFromRowClasses(string classes, bool expected)
    {
        Assert.Equal(expected, BidRow.IsRegistered(classes));
    }

    [Theory]
    [InlineData("listFUTItem has-auction-data highest-bid", true)]
    [InlineData("listFUTItem has-auction-data outbid", false)]
    [InlineData("listFUTItem has-auction-data", false)]
    public void IsOursIsTrueOnlyWhenWeAreHighestBidder(string classes, bool expected)
    {
        Assert.Equal(expected, BidRow.IsOurs(classes));
    }
}
