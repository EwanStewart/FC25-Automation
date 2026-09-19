using Automation.Sbc.Fulfilment;

namespace Automation.Tests;

public class FulfilmentCeilingTests
{
    [Fact]
    public void TheRunCeilingIsTheApprovedCostPlusHalf()
    {
        Assert.Equal(15000, SpendLedger.CeilingFor(10000));
        Assert.Equal(3, SpendLedger.CeilingFor(2));
        Assert.Equal(0, SpendLedger.CeilingFor(0));
    }

    [Fact]
    public void ABidThatWouldPassTheRunCeilingIsRefused()
    {
        SpendLedger ledger = new(1500, []);

        ledger.Commit(0, 1000);

        Assert.True(ledger.Allows(1, 500));
        Assert.False(ledger.Allows(1, 501));
        Assert.Equal(500, ledger.Remaining);
    }

    [Fact]
    public void RaisingTheBidOnOneGapOnlyCountsTheDifference()
    {
        SpendLedger ledger = new(1000, [(0, 600L)]);

        Assert.Equal(600, ledger.Committed);
        Assert.True(ledger.Allows(0, 1000));
        Assert.False(ledger.Allows(0, 1001));
        Assert.False(ledger.Allows(1, 401));
    }

    [Fact]
    public void StandingBidsCarriedInFromAnEarlierRunCountAgainstTheCeiling()
    {
        SpendLedger ledger = new(2000, [(0, 900L), (3, 900L)]);

        Assert.Equal(1800, ledger.Committed);
        Assert.Equal(200, ledger.Remaining);
        Assert.False(ledger.Allows(5, 300));
    }

    [Fact]
    public void ThePerCardCeilingIsTheEstimatePlusAQuarterCappedAtAThousand()
    {
        Assert.Equal(1250, CardCeiling.For(1000));
        Assert.Equal(11000, CardCeiling.For(10000));
        Assert.Equal(101000, CardCeiling.For(100000));
    }

    [Fact]
    public void ThePerCardCeilingNeverFallsBelowTheMarketFloor()
    {
        Assert.Equal(CardCeiling.MINIMUM_COINS, CardCeiling.For(0));
        Assert.Equal(CardCeiling.MINIMUM_COINS, CardCeiling.For(100));
    }

    [Fact]
    public void ThePerCardCeilingIsTrimmedToWhatIsLeftOfTheRunCeiling()
    {
        SpendLedger ledger = new(1000, [(0, 800L)]);

        Assert.Equal(200, CardCeiling.Affordable(CardCeiling.For(5000), ledger, 1));
        Assert.Equal(0, CardCeiling.Affordable(CardCeiling.For(5000), new SpendLedger(0, []), 1));
    }
}
