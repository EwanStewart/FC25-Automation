using Automation.Trading;

namespace Automation.Tests;

public class PricingTests
{
    [Theory]
    [InlineData(750u, 50u)]
    [InlineData(1000u, 100u)]
    [InlineData(9999u, 100u)]
    [InlineData(10000u, 250u)]
    [InlineData(60000u, 500u)]
    [InlineData(150000u, 1000u)]
    public void BidIncrementFollowsMarketBands(uint price, uint expected)
    {
        Assert.Equal(expected, Pricing.BidIncrement(price));
    }

    [Theory]
    [InlineData(200u, 50u, 100u)]
    [InlineData(1000u, 50u, 900u)]
    [InlineData(1000u, 0u, 950u)]
    [InlineData(60u, 100u, 0u)]
    [InlineData(12000u, 200u, 11000u)]
    public void MaxBidIsBreakEvenAfterTaxAndMarginRoundedDown(uint resale, uint margin, uint expected)
    {
        Assert.Equal(expected, Pricing.MaxBid(resale, margin));
    }

    [Theory]
    [InlineData(1000u, 900u)]
    [InlineData(200u, 150u)]
    [InlineData(50u, 0u)]
    public void ListingPriceIsOneIncrementUnderLowestBuyNow(uint lowestBuyNow, uint expected)
    {
        Assert.Equal(expected, Pricing.ListingPrice(lowestBuyNow));
    }

    [Theory]
    [InlineData(1000u, 950u, true)]
    [InlineData(1000u, 951u, false)]
    [InlineData(200u, 150u, true)]
    public void IsProfitableComparesSaleAfterTaxWithCost(uint sellPrice, uint paidPrice, bool expected)
    {
        Assert.Equal(expected, Pricing.IsProfitable(sellPrice, paidPrice));
    }

    [Theory]
    [InlineData("<1 Minute", 0u)]
    [InlineData("1 Minute", 1u)]
    [InlineData("38 Minutes", 38u)]
    [InlineData("1 Hour", 60u)]
    [InlineData("2 Hours", 120u)]
    [InlineData("Expired", 0u)]
    public void ParseMinutesRemainingReadsAuctionTimeText(string text, uint expected)
    {
        Assert.Equal(expected, Pricing.ParseMinutesRemaining(text));
    }

    [Fact]
    public void ParseMinutesRemainingReturnsNullForUnknownText()
    {
        Assert.Null(Pricing.ParseMinutesRemaining(""));
        Assert.Null(Pricing.ParseMinutesRemaining("soon"));
    }

    [Theory]
    [InlineData(5u, true)]
    [InlineData(2u, true)]
    [InlineData(20u, true)]
    [InlineData(1u, false)]
    [InlineData(21u, false)]
    public void IsWithinBidWindowIsInclusive(uint minutes, bool expected)
    {
        Assert.Equal(expected, Pricing.IsWithinBidWindow(minutes, 2, 20));
    }

    [Fact]
    public void ResaleFromAsksIsSecondLowestWithEnoughListings()
    {
        Assert.Equal(0u, Pricing.ResaleFromAsks(new uint[] { 5000 }, 3));
        Assert.Equal(0u, Pricing.ResaleFromAsks(new uint[] { 5000, 4800 }, 3));
        Assert.Equal(250u, Pricing.ResaleFromAsks(new uint[] { 300, 200, 250 }, 3));
        Assert.Equal(200u, Pricing.ResaleFromAsks(new uint[] { 200, 200, 5000 }, 3));
        Assert.Equal(1000u, Pricing.ResaleFromAsks(new uint[] { 50, 1000, 1000, 5000 }, 3));
    }

    [Fact]
    public void ResaleFromAsksIgnoresAsksAtThePriceCeiling()
    {
        Assert.Equal(1300u, Pricing.ResaleFromAsks(new uint[] { 5000, 1100, 5000, 1300, 1800, 4000 }, 3, 4800));
        Assert.Equal(0u, Pricing.ResaleFromAsks(new uint[] { 400, 5000, 5000, 5000 }, 3, 4800));
        Assert.Equal(5000u, Pricing.ResaleFromAsks(new uint[] { 400, 5000, 5000, 5000 }, 3));
    }

    [Theory]
    [InlineData(150u, 1000u, 1211u)]
    [InlineData(500u, 1000u, 1579u)]
    public void RequiredResaleCoversBidMarginAndTax(uint bid, uint margin, uint expected)
    {
        Assert.Equal(expected, Pricing.RequiredResale(bid, margin));
    }

    [Theory]
    [InlineData(500u, 550u)]
    [InlineData(250u, 300u)]
    [InlineData(2000u, 2200u)]
    public void BreakEvenListingCoversCostAfterTaxOnTheIncrementGrid(uint cost, uint expected)
    {
        Assert.Equal(expected, Pricing.BreakEvenListing(cost));
    }

    [Fact]
    public void ListingPricesUndercutMarketUnlessThatWouldLoseMoney()
    {
        Assert.Equal((850u, 900u), Pricing.ListingPrices(1000, null));
        Assert.Equal((850u, 900u), Pricing.ListingPrices(1000, 500));
        Assert.Equal((550u, 600u), Pricing.ListingPrices(400, 500));
    }

    [Fact]
    public void EstimateResaleIsMedianOfLowestSightings()
    {
        Assert.Equal(200u, Pricing.EstimateResale(new uint[] { 400, 200, 200, 600 }, 3));
        Assert.Equal(600u, Pricing.EstimateResale(new uint[] { 500, 700 }, 3));
        Assert.Equal(450u, Pricing.EstimateResale(new uint[] { 450 }, 3));
    }

    [Fact]
    public void EstimateResaleIsNullWithoutSightings()
    {
        Assert.Null(Pricing.EstimateResale(Array.Empty<uint>(), 3));
    }

    [Fact]
    public void IsStaleComparesAgeAgainstLimit()
    {
        DateTime now = new(2026, 9, 18, 12, 0, 0);

        Assert.False(Pricing.IsStale(now.AddHours(-5), now, 6));
        Assert.True(Pricing.IsStale(now.AddHours(-7), now, 6));
    }

    [Theory]
    [InlineData(10000u, 0u, 500u, 0.5, true)]
    [InlineData(10000u, 4600u, 500u, 0.5, false)]
    [InlineData(10000u, 4500u, 500u, 0.5, true)]
    public void FitsExposureLimitCapsCommittedCoins(uint balance, uint committed, uint bid, double share,
        bool expected)
    {
        Assert.Equal(expected, Pricing.FitsExposureLimit(balance, committed, bid, share));
    }
}
