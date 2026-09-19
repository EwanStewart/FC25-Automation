using Automation.Sbc.Fulfilment;
using Automation.Trading;
using static Automation.Tests.FulfilmentFixtures;

namespace Automation.Tests;

public class FulfilmentBuyingTests
{
    private static (BuyingResult result, RecordingStore store, List<string> log) Buy(FulfilmentRun run,
        IReadOnlyList<GapRecord> gaps, Dictionary<int, IReadOnlyList<AuctionListing>> results,
        Dictionary<string, BidOutcome>? outcomes = null, IReadOnlyList<TradeState>? standing = null)
    {
        List<string> log = [];
        RecordingStore store = new(log);
        ScriptedMarket market = new(log, results, outcomes, standing);
        GapBuyer buyer = new(market, store);

        return (buyer.Buy(run, gaps), store, log);
    }

    [Fact]
    public void ADryRunRecordsWhatItWouldBuyAndPlacesNoBid()
    {
        var (result, store, log) = Buy(Run(true, 3000), [Gap(0)],
            new Dictionary<int, IReadOnlyList<AuctionListing>> { [0] = [Listing("t1", 800)] });

        Assert.DoesNotContain(log, entry => entry.StartsWith("bid"));
        Assert.Equal(GapOutcome.Simulated, store.Gaps(1)[0].Outcome);
        Assert.True(store.Gaps(1)[0].Simulated);
        Assert.Equal(800, store.Gaps(1)[0].BidAmount);
        Assert.Equal(FulfilmentState.Building, result.State);
    }

    [Fact]
    public void TheRunCeilingStopsTheWholeFulfilmentRatherThanBiddingOn()
    {
        var (result, store, log) = Buy(Run(false, 1000), [Gap(0), Gap(1)],
            new Dictionary<int, IReadOnlyList<AuctionListing>>
            {
                [0] = [Listing("t1", 900)],
                [1] = [Listing("t2", 900)]
            });

        Assert.Equal(FulfilmentState.Aborted, result.State);
        Assert.Contains("ceiling", result.Detail);
        Assert.Single(log.Where(entry => entry.StartsWith("bid")));
        Assert.Equal(GapOutcome.Bidding, store.Gaps(1)[0].Outcome);
    }

    [Fact]
    public void ADryRunHitsTheSameCeilingTheLiveRunWould()
    {
        var (result, _, log) = Buy(Run(true, 1000), [Gap(0), Gap(1)],
            new Dictionary<int, IReadOnlyList<AuctionListing>>
            {
                [0] = [Listing("t1", 900)],
                [1] = [Listing("t2", 900)]
            });

        Assert.Equal(FulfilmentState.Aborted, result.State);
        Assert.Single(log.Where(entry => entry.StartsWith("search")));
    }

    [Fact]
    public void AMarketOnlyOfferingCardsAboveThePerCardCeilingBuysNothing()
    {
        var (result, store, log) = Buy(Run(false, 100000), [Gap(0, 900)],
            new Dictionary<int, IReadOnlyList<AuctionListing>> { [0] = [Listing("t1", 4000)] });

        Assert.DoesNotContain(log, entry => entry.StartsWith("bid"));
        Assert.Equal(GapOutcome.TooExpensive, store.Gaps(1)[0].Outcome);
        Assert.Equal(FulfilmentState.Buying, result.State);
    }

    [Fact]
    public void ThePerCardCeilingIsWhatTheSearchAsksFor()
    {
        List<string> log = [];
        ScriptedMarket market = new(log, new Dictionary<int, IReadOnlyList<AuctionListing>>());
        GapBuyer buyer = new(market, new RecordingStore(log));

        buyer.Buy(Run(false, 100000), [Gap(0, 900)]);

        Assert.Equal(CardCeiling.For(900), (int)market.Ceilings[0]);
    }

    [Fact]
    public void AnEmptyMarketIsRecordedAsNotFound()
    {
        var (result, store, _) = Buy(Run(false, 100000), [Gap(0)],
            new Dictionary<int, IReadOnlyList<AuctionListing>> { [0] = [] });

        Assert.Equal(GapOutcome.NotFound, store.Gaps(1)[0].Outcome);
        Assert.Equal(FulfilmentState.Buying, result.State);
    }

    [Fact]
    public void ABidIsWrittenDownBeforeItIsPlaced()
    {
        var (_, _, log) = Buy(Run(false, 100000), [Gap(0)],
            new Dictionary<int, IReadOnlyList<AuctionListing>> { [0] = [Listing("t1", 800)] });

        var written = log.FindIndex(entry => entry == "gap 0 Attempting 800");
        var placed = log.FindIndex(entry => entry.StartsWith("bid"));

        Assert.True(written >= 0);
        Assert.True(written < placed);
    }

    [Fact]
    public void AGapAlreadyWonIsNeverSearchedForAgain()
    {
        var won = Gap(0) with { Outcome = GapOutcome.Won, TradeId = "t1", BidAmount = 800, FinalPrice = 800 };
        var (result, store, log) = Buy(Run(false, 100000), [won],
            new Dictionary<int, IReadOnlyList<AuctionListing>> { [0] = [Listing("t9", 400)] });

        Assert.Empty(log.Where(entry => entry.StartsWith("search")));
        Assert.Equal(GapOutcome.Won, store.Gaps(1)[0].Outcome);
        Assert.Equal(FulfilmentState.Building, result.State);
    }

    [Fact]
    public void AGapWeWereOutbidOnIsBidAgain()
    {
        var outbid = Gap(0) with { Outcome = GapOutcome.Outbid, TradeId = "t1", BidAmount = 800 };
        var (_, store, log) = Buy(Run(false, 100000), [outbid],
            new Dictionary<int, IReadOnlyList<AuctionListing>> { [0] = [Listing("t2", 900)] });

        Assert.Contains("bid t2 900", log);
        Assert.Equal(GapOutcome.Bidding, store.Gaps(1)[0].Outcome);
    }

    [Fact]
    public void AnExpiredAuctionFreesTheGapAndTheNextRunBuysAgain()
    {
        var bidding = Gap(0) with { Outcome = GapOutcome.Bidding, TradeId = "t1", BidAmount = 800 };
        var (_, store, log) = Buy(Run(false, 100000), [bidding],
            new Dictionary<int, IReadOnlyList<AuctionListing>> { [0] = [Listing("t2", 700)] },
            standing: [new TradeState("t1", "expired", "outbid", 1200)]);

        Assert.Contains("bid t2 700", log);
        Assert.Equal(GapOutcome.Bidding, store.Gaps(1)[0].Outcome);
        Assert.Equal("t2", store.Gaps(1)[0].TradeId);
    }

    [Fact]
    public void AnAuctionWeWonIsBankedAndNotBoughtAgain()
    {
        var bidding = Gap(0) with { Outcome = GapOutcome.Bidding, TradeId = "t1", BidAmount = 800 };
        var (result, store, log) = Buy(Run(false, 100000), [bidding],
            new Dictionary<int, IReadOnlyList<AuctionListing>> { [0] = [Listing("t2", 700)] },
            standing: [new TradeState("t1", "closed", "highest", 800)]);

        Assert.Empty(log.Where(entry => entry.StartsWith("search")));
        Assert.Equal(GapOutcome.Won, store.Gaps(1)[0].Outcome);
        Assert.Equal(FulfilmentState.Building, result.State);
    }

    [Fact]
    public void AStandingBidStillRunningLeavesTheRunWaitingWithoutASecondCard()
    {
        var bidding = Gap(0) with { Outcome = GapOutcome.Bidding, TradeId = "t1", BidAmount = 800 };
        var (result, _, log) = Buy(Run(false, 100000), [bidding],
            new Dictionary<int, IReadOnlyList<AuctionListing>> { [0] = [Listing("t2", 700)] },
            standing: [new TradeState("t1", "active", "highest", 800)]);

        Assert.Empty(log.Where(entry => entry.StartsWith("bid")));
        Assert.Equal(FulfilmentState.Buying, result.State);
    }

    [Fact]
    public void ABidWrittenDownThatCannotBeReadBackStopsTheRunBeforeAnySearch()
    {
        var attempting = Gap(0) with { Outcome = GapOutcome.Attempting, TradeId = "t1", BidAmount = 800 };
        var (result, store, log) = Buy(Run(false, 100000), [attempting, Gap(1)],
            new Dictionary<int, IReadOnlyList<AuctionListing>> { [0] = [Listing("t2", 700)] });

        Assert.Empty(log.Where(entry => entry.StartsWith("search")));
        Assert.Equal(FulfilmentState.Failed, result.State);
        Assert.Equal(GapOutcome.Unresolved, store.Gaps(1)[0].Outcome);
    }

    [Fact]
    public void ABidTheMarketRefusedStopsTheRun()
    {
        var (result, store, _) = Buy(Run(false, 100000), [Gap(0), Gap(1)],
            new Dictionary<int, IReadOnlyList<AuctionListing>>
            {
                [0] = [Listing("t1", 800)],
                [1] = [Listing("t2", 800)]
            },
            new Dictionary<string, BidOutcome> { ["t1"] = BidOutcome.Failed });

        Assert.Equal(FulfilmentState.Failed, result.State);
        Assert.Equal(GapOutcome.Unresolved, store.Gaps(1)[0].Outcome);
        Assert.Equal(GapOutcome.Pending, store.Gaps(1)[1].Outcome);
    }

    [Fact]
    public void ABidOvertakenAsItLandedLeavesTheGapOutbid()
    {
        var (result, store, _) = Buy(Run(false, 100000), [Gap(0)],
            new Dictionary<int, IReadOnlyList<AuctionListing>> { [0] = [Listing("t1", 800)] },
            new Dictionary<string, BidOutcome> { ["t1"] = BidOutcome.Overtaken });

        Assert.Equal(GapOutcome.Outbid, store.Gaps(1)[0].Outcome);
        Assert.Equal(FulfilmentState.Buying, result.State);
    }

    [Fact]
    public void ASecondRunOverTheSameApprovalBuysNothingTwice()
    {
        List<string> log = [];
        RecordingStore store = new(log);
        Dictionary<int, IReadOnlyList<AuctionListing>> results = new()
        {
            [0] = [Listing("t1", 800)],
            [1] = [Listing("t1", 800)]
        };
        ScriptedMarket first = new(log, results);
        GapBuyer buyer = new(first, store);

        buyer.Buy(Run(false, 100000), [Gap(0)]);

        ScriptedMarket second = new(log, results, null, [new TradeState("t1", "closed", "highest", 800)]);
        var result = new GapBuyer(second, store).Buy(Run(false, 100000), store.Gaps(1));

        Assert.Single(log.Where(entry => entry.StartsWith("bid")));
        Assert.Equal(FulfilmentState.Building, result.State);
        Assert.Equal(800, store.Gaps(1)[0].FinalPrice);
    }
}
