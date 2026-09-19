using Automation.Sbc.Fulfilment;
using static Automation.Tests.FulfilmentFixtures;

namespace Automation.Tests;

public class FulfilmentProgressTests
{
    private static ApprovalSlot Owned(int slot)
    {
        return new ApprovalSlot(slot, "CB", 100 + slot, $"Owned {slot}", 70, true, null, 0);
    }

    private static ApprovalSlot Bought(int slot)
    {
        return new ApprovalSlot(slot, "CB", null, "Buy: silver CB", 68, false, "silver CB", 900);
    }

    private static PlacementRecord Placed(int slot)
    {
        return new PlacementRecord(slot, "CB", SquadPlan.FROM_CLUB, 100 + slot, $"Owned {slot}",
            PlacementOutcome.Placed);
    }

    [Fact]
    public void ProgressCountsThePlacedSlotsAgainstTheWholeSquad()
    {
        var progress = FulfilmentProgress.Of([Owned(0), Owned(1), Bought(2)], [Gap(2)], [Placed(0), Placed(1)]);

        Assert.Equal(2, progress.Placed);
        Assert.Equal(3, progress.Wanted);
        Assert.Equal(1, progress.Outstanding);
    }

    [Fact]
    public void APlacementForASlotOutsideTheApprovalIsNotCounted()
    {
        var progress = FulfilmentProgress.Of([Owned(0)], [], [Placed(0), Placed(9)]);

        Assert.Equal(1, progress.Placed);
    }

    [Fact]
    public void ASimulatedPlacementCountsSoADryRunCanShowAFullSquad()
    {
        var simulated = new PlacementRecord(0, "CB", SquadPlan.FROM_CLUB, 100, "Owned 0",
            PlacementOutcome.Simulated, true, "would place");

        var progress = FulfilmentProgress.Of([Owned(0)], [], [simulated]);

        Assert.Equal(1, progress.Placed);
        Assert.True(FulfilmentProgress.Complete(progress));
    }

    [Fact]
    public void AMissingPlacementDoesNotCount()
    {
        var missing = new PlacementRecord(0, "CB", SquadPlan.FROM_CLUB, 100, "Owned 0", PlacementOutcome.Missing,
            false, "the slot did not take the card");

        Assert.Equal(0, FulfilmentProgress.Of([Owned(0)], [], [missing]).Placed);
    }

    [Fact]
    public void ASettledGapIsNoLongerOutstanding()
    {
        var won = Gap(0) with { Outcome = GapOutcome.Won, ItemId = 99 };

        var progress = FulfilmentProgress.Of([Bought(0)], [won], [Placed(0)]);

        Assert.Equal(0, progress.Outstanding);
        Assert.True(FulfilmentProgress.Complete(progress));
    }

    [Fact]
    public void AnEmptyApprovalIsNeverComplete()
    {
        Assert.False(FulfilmentProgress.Complete(FulfilmentProgress.Of([], [], [])));
    }

    [Fact]
    public void ASquadStillMissingACardIsNotComplete()
    {
        var progress = FulfilmentProgress.Of([Owned(0), Bought(1)], [Gap(1)], [Placed(0)]);

        Assert.False(FulfilmentProgress.Complete(progress));
    }

    [Fact]
    public void TheWaitingLineNamesTheSlotAndWhyItIsStillOpen()
    {
        var progress = FulfilmentProgress.Of([Bought(0)], [Gap(0) with { Outcome = GapOutcome.NotFound }], []);

        Assert.Contains("slot 0", progress.WaitingOn);
        Assert.Contains("not on the market", progress.WaitingOn);
    }

    [Fact]
    public void SlotsWaitingOnTheSameThingAreNamedTogether()
    {
        var progress = FulfilmentProgress.Of([Bought(0), Bought(1), Bought(2)],
            [Gap(0) with { Outcome = GapOutcome.NotFound }, Gap(1) with { Outcome = GapOutcome.NotFound },
                Gap(2) with { Outcome = GapOutcome.Bidding }], []);

        Assert.Contains("slots 0, 1", progress.WaitingOn);
        Assert.Contains("slot 2 bidding", progress.WaitingOn);
    }

    [Fact]
    public void NothingOutstandingSaysSoRatherThanNamingSlots()
    {
        var progress = FulfilmentProgress.Of([Owned(0)], [], [Placed(0)]);

        Assert.DoesNotContain("slot", progress.WaitingOn);
        Assert.Contains("nothing left to buy", progress.WaitingOn);
    }

    [Fact]
    public void TheDescriptionReadsAsASentenceAHumanCanActOn()
    {
        var progress = FulfilmentProgress.Of([Owned(0), Bought(1)], [Gap(1)], [Placed(0)]);

        Assert.Equal("1 of 2 slots placed, 1 gap outstanding, waiting on slot 1 still to buy",
            FulfilmentProgress.Describe(progress));
    }

    [Fact]
    public void ManyOutstandingGapsAreCountedInThePlural()
    {
        var progress = FulfilmentProgress.Of([Bought(0), Bought(1)], [Gap(0), Gap(1)], []);

        Assert.Contains("2 gaps outstanding", FulfilmentProgress.Describe(progress));
    }
}
