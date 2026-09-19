using Automation.Sbc.Fulfilment;
using Automation.Trading;
using static Automation.Tests.FulfilmentFixtures;

namespace Automation.Tests;

public class FulfilmentOrderTests
{
    private static ApprovalSlot Owned(int slot, long id)
    {
        return new ApprovalSlot(slot, "CB", id, $"Owned {id}", 70, true, null, 0);
    }

    private static ApprovalSlot Bought(int slot)
    {
        return new ApprovalSlot(slot, "CB", null, "Buy: silver CB", 68, false, "silver CB", 900);
    }

    private static SquadSlotView Empty(int slot)
    {
        return new SquadSlotView(slot, "CB", 0, 0, 0, false);
    }

    private static RecordingStore Store(List<string> log, params GapRecord[] gaps)
    {
        RecordingStore store = new(log);

        store.Runs.Add(Run(FulfilmentMode.Dry, 100000));

        foreach (var gap in gaps) store.SaveGap(1, gap);

        log.Clear();

        return store;
    }

    private static void Drive(RecordingStore store, IReadOnlyList<ApprovalSlot> slots, ISquadAgent squad,
        IMarketAgent market, FulfilmentMode mode)
    {
        FulfilmentProgram.Process(store, _ => slots, _ => market, _ => squad, mode);
    }

    [Fact]
    public void TheClubCardsGoIntoTheSquadBeforeTheMarketIsTouched()
    {
        List<string> log = [];
        var store = Store(log, Gap(2));
        ScriptedMarket market = new(log, new Dictionary<int, IReadOnlyList<AuctionListing>>
        {
            [0] = [Listing("t1", 800)]
        });
        ScriptedSquad squad = new(log, [Empty(0), Empty(1), Empty(2)]);

        Drive(store, [Owned(0, 11), Owned(1, 12), Bought(2)], squad, market, FulfilmentMode.Live);

        var firstPlacement = log.FindIndex(entry => entry.StartsWith("place-in-app"));
        var firstSearch = log.FindIndex(entry => entry.StartsWith("search"));

        Assert.True(firstPlacement >= 0);
        Assert.True(firstSearch > firstPlacement);
    }

    [Fact]
    public void TheCardThatWasJustWonIsPlacedInTheSameRun()
    {
        List<string> log = [];
        var store = Store(log, Gap(1) with { Outcome = GapOutcome.Bidding, TradeId = "t1", BidAmount = 800,
            ItemId = 99 });
        ScriptedMarket market = new(log, new Dictionary<int, IReadOnlyList<AuctionListing>>(), null,
            [new TradeState("t1", "closed", "highest", 800)]);
        ScriptedSquad squad = new(log, [Empty(0), Empty(1)]);

        Drive(store, [Owned(0, 11), Bought(1)], squad, market, FulfilmentMode.Live);

        Assert.Contains("place-in-app 1 99", log);
        Assert.Equal(PlacementOutcome.Placed, store.Placements(1)[1].Outcome);
        Assert.Equal(FulfilmentState.Built, store.State);
    }

    [Fact]
    public void AnOutstandingGapLeavesTheRunBuyingWithTheClubCardsAlreadyIn()
    {
        List<string> log = [];
        var store = Store(log, Gap(1));
        ScriptedMarket market = new(log, new Dictionary<int, IReadOnlyList<AuctionListing>> { [0] = [] });
        ScriptedSquad squad = new(log, [Empty(0), Empty(1)]);

        Drive(store, [Owned(0, 11), Bought(1)], squad, market, FulfilmentMode.Live);

        Assert.Equal(FulfilmentState.Buying, store.State);
        Assert.Equal(PlacementOutcome.Placed, store.Placements(1)[0].Outcome);
        Assert.Contains("1 of 2 slots placed", store.Detail);
        Assert.Contains("slot 1", store.Detail);
    }

    [Fact]
    public void ASecondRunPlacesTheWonCardWithoutBuyingOrPlacingAnythingTwice()
    {
        List<string> log = [];
        var store = Store(log, Gap(1));
        ScriptedSquad squad = new(log, [Empty(0), Empty(1)]);
        ScriptedMarket first = new(log, new Dictionary<int, IReadOnlyList<AuctionListing>>
        {
            [0] = [Listing("t1", 800)]
        });

        Drive(store, [Owned(0, 11), Bought(1)], squad, first, FulfilmentMode.Live);

        ScriptedMarket second = new(log, new Dictionary<int, IReadOnlyList<AuctionListing>>(), null,
            [new TradeState("t1", "closed", "highest", 800)]);

        Drive(store, [Owned(0, 11), Bought(1)], squad, second, FulfilmentMode.Live);

        Assert.Equal(1, log.Count(entry => entry.StartsWith("bid")));
        Assert.Equal(2, log.Count(entry => entry.StartsWith("place-in-app")));
        Assert.Equal(FulfilmentState.Built, store.State);
    }

    [Fact]
    public void ASquadThatRefusesAClubCardStopsTheRunBeforeAnyBidding()
    {
        List<string> log = [];
        var store = Store(log, Gap(1));
        ScriptedMarket market = new(log, new Dictionary<int, IReadOnlyList<AuctionListing>>
        {
            [0] = [Listing("t1", 800)]
        });
        ScriptedSquad squad = new(log, [Empty(0), Empty(1)]) { Refuse = 0 };

        Drive(store, [Owned(0, 11), Bought(1)], squad, market, FulfilmentMode.Live);

        Assert.DoesNotContain(log, entry => entry.StartsWith("search"));
        Assert.DoesNotContain(log, entry => entry.StartsWith("bid"));
        Assert.Equal(FulfilmentState.Failed, store.State);
    }

    [Fact]
    public void ADryRunPlacesNothingBidsNothingAndStaysQueued()
    {
        List<string> log = [];
        var store = Store(log, Gap(1));
        ScriptedMarket market = new(log, new Dictionary<int, IReadOnlyList<AuctionListing>>
        {
            [0] = [Listing("t1", 800)]
        });
        ScriptedSquad squad = new(log, [Empty(0), Empty(1)]);

        Drive(store, [Owned(0, 11), Bought(1)], squad, market, FulfilmentMode.Dry);

        Assert.DoesNotContain(log, entry => entry.StartsWith("place-in-app"));
        Assert.DoesNotContain(log, entry => entry.StartsWith("bid"));
        Assert.Equal(FulfilmentState.Pending, store.State);
        Assert.Contains("dry run reached Built", store.Detail);
        Assert.All(store.Placements(1), placement => Assert.True(placement.Simulated));
    }

    [Fact]
    public void ADryRunStillWalksIntoTheChallengeSoTheRouteIsExercised()
    {
        List<string> log = [];
        var store = Store(log, Gap(1));
        ScriptedMarket market = new(log, new Dictionary<int, IReadOnlyList<AuctionListing>> { [0] = [] });
        ScriptedSquad squad = new(log, [Empty(0), Empty(1)]);

        Drive(store, [Owned(0, 11), Bought(1)], squad, market, FulfilmentMode.Dry);

        Assert.Contains("open 1234", log);
    }

    [Fact]
    public void AChallengeThatNeverOpensLeavesADryRunQueuedRatherThanFailed()
    {
        List<string> log = [];
        var store = Store(log, Gap(1));
        ScriptedMarket market = new(log, new Dictionary<int, IReadOnlyList<AuctionListing>>());
        RefusingSquad squad = new();

        Drive(store, [Owned(0, 11), Bought(1)], squad, market, FulfilmentMode.Dry);

        Assert.Equal(FulfilmentState.Pending, store.State);
        Assert.DoesNotContain(log, entry => entry.StartsWith("search"));
    }

    [Fact]
    public void AChallengeThatNeverOpensFailsALiveRunWithoutSpendingAnything()
    {
        List<string> log = [];
        var store = Store(log, Gap(1));
        ScriptedMarket market = new(log, new Dictionary<int, IReadOnlyList<AuctionListing>>());
        RefusingSquad squad = new();

        Drive(store, [Owned(0, 11), Bought(1)], squad, market, FulfilmentMode.Live);

        Assert.Equal(FulfilmentState.Failed, store.State);
        Assert.DoesNotContain(log, entry => entry.StartsWith("search"));
    }
}
