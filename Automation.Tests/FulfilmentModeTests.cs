using Automation.Sbc.Fulfilment;
using Automation.Trading;
using static Automation.Tests.FulfilmentFixtures;

namespace Automation.Tests;

public class FulfilmentModeTests
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

    private static RecordingStore Store(List<string> log)
    {
        RecordingStore store = new(log);

        store.Runs.Add(Run(FulfilmentMode.Dry, 100000));
        store.SaveGap(1, Gap(1));
        log.Clear();

        return store;
    }

    private static ScriptedMarket Market(List<string> log)
    {
        return new ScriptedMarket(log, new Dictionary<int, IReadOnlyList<AuctionListing>>
        {
            [0] = [Listing("t1", 800)]
        });
    }

    private static void Drive(RecordingStore store, ISquadAgent squad, IMarketAgent market, FulfilmentMode mode)
    {
        FulfilmentProgram.Process(store, _ => [Owned(0, 11), Bought(1)], _ => market, _ => squad, mode);
    }

    [Fact]
    public void NoFlagAtAllChoosesTheDryMode()
    {
        Assert.Equal(FulfilmentMode.Dry, FulfilmentModes.Chosen(false, false));
        Assert.Equal(FulfilmentMode.PlaceLive, FulfilmentModes.Chosen(false, true));
        Assert.Equal(FulfilmentMode.Live, FulfilmentModes.Chosen(true, false));
        Assert.Equal(FulfilmentMode.Live, FulfilmentModes.Chosen(true, true));
    }

    [Fact]
    public void TheDryModePlacesNothingAndBidsNothing()
    {
        List<string> log = [];
        var store = Store(log);
        ScriptedSquad squad = new(log, [Empty(0), Empty(1)]);

        Drive(store, squad, Market(log), FulfilmentMode.Dry);

        Assert.DoesNotContain(log, entry => entry.StartsWith("place-in-app"));
        Assert.DoesNotContain(log, entry => entry.StartsWith("bid"));
        Assert.All(store.Placements(1), placement => Assert.True(placement.Simulated));
        Assert.Equal(FulfilmentState.Pending, store.State);
    }

    [Fact]
    public void ThePlaceLiveModePutsTheClubCardsInAndStillBidsNothing()
    {
        List<string> log = [];
        var store = Store(log);
        ScriptedSquad squad = new(log, [Empty(0), Empty(1)]);

        Drive(store, squad, Market(log), FulfilmentMode.PlaceLive);

        Assert.Contains("place-in-app 0 11", log);
        Assert.DoesNotContain(log, entry => entry.StartsWith("bid"));
        Assert.False(store.Placements(1)[0].Simulated);
        Assert.Equal(PlacementOutcome.Placed, store.Placements(1)[0].Outcome);
        Assert.Equal(GapOutcome.Simulated, store.Gaps(1)[0].Outcome);
    }

    [Fact]
    public void TheLiveModePlacesTheClubCardsAndBidsForTheGap()
    {
        List<string> log = [];
        var store = Store(log);
        ScriptedSquad squad = new(log, [Empty(0), Empty(1)]);
        var market = Market(log);

        market.Polls.Enqueue([new TradeState("t1", "active", "none", 0, 10, 800, 77)]);

        Drive(store, squad, market, FulfilmentMode.Live);

        Assert.Contains("place-in-app 0 11", log);
        Assert.Contains("watch t1", log);
        Assert.Contains(log, entry => entry.StartsWith("bid t1"));
        Assert.Equal(GapOutcome.Bidding, store.Gaps(1)[0].Outcome);
    }

    [Fact]
    public void ThePlaceLiveModeNeverReachesTheBidPath()
    {
        List<string> log = [];
        var store = Store(log);
        ScriptedSquad squad = new(log, [Empty(0), Empty(1)]);
        RefusingMarket market = new();

        Drive(store, squad, market, FulfilmentMode.PlaceLive);

        Assert.Equal(0, market.Bids);
        Assert.Contains("place-in-app 0 11", log);
        Assert.Equal(GapOutcome.Simulated, store.Gaps(1)[0].Outcome);
    }

    [Fact]
    public void AMarketHandedToARunThatIsNotBuyingLiveRefusesToBid()
    {
        RefusingMarket inner = new();
        SimulatedMarket market = new(inner);
        var listing = Listing("t1", 800);

        Assert.Single(market.Search(Specification(), 900).Listings);
        Assert.Throws<InvalidOperationException>(() =>
            market.Bid(new MarketChoice(listing, 800), 800));
        Assert.Equal(0, inner.Bids);
    }

    [Fact]
    public void ACardThePlaceLiveRunOnlySimulatedBuyingIsNeverPlacedForReal()
    {
        List<string> log = [];
        var store = Store(log);
        ScriptedSquad squad = new(log, [Empty(0), Empty(1)]);

        Drive(store, squad, Market(log), FulfilmentMode.PlaceLive);

        Assert.DoesNotContain("place-in-app 1 ", string.Join("|", log));
        Assert.True(store.Placements(1)[1].Simulated);
        Assert.Equal(PlacementOutcome.Simulated, store.Placements(1)[1].Outcome);
    }

    [Fact]
    public void ARunThatDidNotBuyLiveStaysQueuedSoALiveRunStillPicksItUp()
    {
        List<string> log = [];
        var store = Store(log);
        ScriptedSquad squad = new(log, [Empty(0), Empty(1)]);

        Drive(store, squad, Market(log), FulfilmentMode.PlaceLive);

        Assert.Equal(FulfilmentState.Pending, store.State);
        Assert.Contains("simulated buying reached", store.Detail);
    }
}
