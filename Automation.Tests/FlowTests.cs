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
    public void NextStepWaitsWhileTheClickShieldIsUp()
    {
        Assert.Equal(LoginStep.Wait, LoginFlow.NextStep(new LoginScreen(false, false, false, false, true, false, true)));
        Assert.Equal(LoginStep.Done, LoginFlow.NextStep(new LoginScreen(true, false, false, false, false, false, true)));
    }

    [Theory]
    [InlineData("Derby County", "Home Kit", "small kit item common", "", "", "Derby County Home Kit")]
    [InlineData("Jagiellonia", "", "small badge item common", "", "", "Jagiellonia Badge")]
    [InlineData("Stach", "", "small player item common ut-item-loaded", "80", "CM", "Stach 80 CM")]
    [InlineData("Mystery", "", "", "", "", "Mystery")]
    public void ItemKeyComesFromTheRowItself(string name, string desc, string classes, string rating, string position,
        string expected)
    {
        Assert.Equal(expected, ItemKey.Build(name, desc, classes, rating, position));
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

    [Theory]
    [InlineData("listFUTItem has-auction-data expired", true)]
    [InlineData("listFUTItem has-auction-data outbid", true)]
    [InlineData("listFUTItem has-auction-data won expired", false)]
    [InlineData("listFUTItem has-auction-data highest-bid", false)]
    [InlineData("listFUTItem has-auction-data", false)]
    public void IsLostMarksExpiredAndOutbidRowsThatWereNotWon(string classes, bool expected)
    {
        Assert.Equal(expected, BidRow.IsLost(classes));
    }

    [Fact]
    public void SelectedRowIsRecognisedByItsClass()
    {
        Assert.True(BidRow.IsSelected("listFUTItem has-auction-data selected"));
        Assert.False(BidRow.IsSelected("listFUTItem has-auction-data"));
    }

    [Fact]
    public void NextStepLeavesTheRecognisedDeviceScreenByAnotherMethod()
    {
        LoginScreen screen = new(false, false, false, false, false, false, false, true, true, true);

        Assert.Equal(LoginStep.UseAnotherMethod, LoginFlow.NextStep(screen, default));
    }

    [Fact]
    public void NextStepSendsACodeRatherThanTryingTheRecognisedDevicePath()
    {
        LoginScreen screen = new(false, false, false, false, false, false, false, false, true, false);

        Assert.Equal(LoginStep.SendCode, LoginFlow.NextStep(screen, default));
    }

    [Fact]
    public void NextStepDoesNotTypeACodeBeforeOneWasRequested()
    {
        LoginScreen screen = new(false, false, false, false, false, false, false, false, true, true);

        Assert.Equal(LoginStep.SendCode, LoginFlow.NextStep(screen, default));
    }

    [Fact]
    public void NextStepTypesTheCodeOnceItHasBeenRequested()
    {
        LoginScreen screen = new(false, false, false, false, false, false, false, false, true, true);
        LoginProgress progress = new(true);

        Assert.Equal(LoginStep.EnterVerificationCode, LoginFlow.NextStep(screen, progress));
    }

    [Fact]
    public void NextStepWaitsOnTheVerificationScreenWhenTheShieldIsUp()
    {
        LoginScreen screen = new(false, false, false, false, false, false, true, true, true, true);

        Assert.Equal(LoginStep.Wait, LoginFlow.NextStep(screen, default));
    }

    [Fact]
    public void NextStepIgnoresVerificationOnceTheWebAppIsUp()
    {
        LoginScreen screen = new(true, false, false, false, false, false, false, true, true, true);

        Assert.Equal(LoginStep.Done, LoginFlow.NextStep(screen, default));
    }
}
