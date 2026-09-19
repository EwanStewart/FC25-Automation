using Automation.Sbc;
using Automation.Sbc.Fulfilment;

namespace Automation.Tests;

public class FulfilmentQueueTests
{
    private static SquadPlayer Player(long id, bool owned)
    {
        return new SquadPlayer(id, 1, owned ? "Owned" : "Buy", 70, "CB", ["CB"], 1, 13, 14, 0, false, 0, owned);
    }

    private static MarketSpecification Specification(int estimate)
    {
        return new MarketSpecification("CB", PlayerQuality.Silver, 65, 74, 14, 13, null, null, estimate);
    }

    private static SolvedSquad Squad()
    {
        SquadSlot[] slots =
        [
            new(0, "CB", Player(11, true), null),
            new(1, "CB", Player(900001, false), Specification(900)),
            new(2, "ST", Player(900002, false), Specification(1500))
        ];

        return new SolvedSquad(SolveOutcome.Solved, "", slots, 2400, 2, null);
    }

    [Fact]
    public void OnlyTheSlotsThatNeedBuyingBecomeGaps()
    {
        var gaps = FulfilmentGaps.From(Squad());

        Assert.Equal(2, gaps.Count);
        Assert.Equal([1, 2], gaps.Select(gap => gap.SlotIndex));
    }

    [Fact]
    public void EveryGapCarriesItsOwnCardCeilingAndStartsPending()
    {
        var gaps = FulfilmentGaps.From(Squad());

        Assert.Equal(CardCeiling.For(900), gaps[0].CardCeiling);
        Assert.Equal(CardCeiling.For(1500), gaps[1].CardCeiling);
        Assert.All(gaps, gap => Assert.Equal(GapOutcome.Pending, gap.Outcome));
        Assert.All(gaps, gap => Assert.True(gap.Simulated));
    }

    [Fact]
    public void ASquadNeedingNoPurchaseQueuesNoGaps()
    {
        SolvedSquad owned = new(SolveOutcome.Solved, "", [new SquadSlot(0, "CB", Player(11, true), null)], 0, 0,
            null);

        Assert.Empty(FulfilmentGaps.From(owned));
    }

    [Fact]
    public void AQueuedRunIsDryUntilSomethingExplicitlyTurnsItLive()
    {
        Assert.True(FulfilmentDefaults.DRY_RUN);
        Assert.True(FulfilmentDefaults.DryRun(false));
        Assert.False(FulfilmentDefaults.DryRun(true));
    }
}
