using System.Reflection;
using Automation.Sbc.Fulfilment;
using static Automation.Tests.FulfilmentFixtures;

namespace Automation.Tests;

public class FulfilmentBuildingTests
{
    private static ApprovalSlot Owned(int slot, long id)
    {
        return new ApprovalSlot(slot, "CB", id, $"Owned {id}", 70, true, null, 0);
    }

    private static ApprovalSlot Bought(int slot)
    {
        return new ApprovalSlot(slot, "CB", null, "Buy: silver CB", 68, false, "silver CB", 900);
    }

    private static GapRecord Won(int slot, long itemId)
    {
        return Gap(slot) with { Outcome = GapOutcome.Won, ItemId = itemId, BidAmount = 800, FinalPrice = 800 };
    }

    [Fact]
    public void OwnedSlotsAndWonCardsBecomeOneOrderedPlan()
    {
        var targets = SquadPlan.Targets([Owned(0, 11), Bought(1)], [Won(1, 99)]);

        Assert.Equal(2, targets.Count);
        Assert.Equal(11, targets[0].ItemId);
        Assert.Equal("club", targets[0].Source);
        Assert.Equal(99, targets[1].ItemId);
        Assert.Equal("bought", targets[1].Source);
    }

    [Fact]
    public void ASlotWhoseCardWasNeverWonLeavesThePlanIncomplete()
    {
        var targets = SquadPlan.Targets([Owned(0, 11), Bought(1)], [Gap(1)]);

        Assert.Equal("slot 1", SquadPlan.Unready(targets));
        Assert.False(SquadPlan.Ready(targets));
    }

    [Fact]
    public void ADryRunPlacesNothingAndWritesDownWhatItWouldPlace()
    {
        List<string> log = [];
        RecordingStore store = new(log);
        ScriptedSquad squad = new(log, [new SquadSlotView(0, "CB", 0, 0, 0, false)]);

        var result = new SquadBuilder(squad, store).Build(Run(true, 3000), [Owned(0, 11)], []);

        Assert.DoesNotContain(log, entry => entry.StartsWith("place-in-app"));
        Assert.Equal(PlacementOutcome.Simulated, store.Placements(1)[0].Outcome);
        Assert.Equal(FulfilmentState.Built, result.State);
    }

    [Fact]
    public void ASlotAlreadyHoldingTheIntendedCardIsLeftAlone()
    {
        List<string> log = [];
        RecordingStore store = new(log);
        ScriptedSquad squad = new(log, [new SquadSlotView(0, "CB", 11, 0, 70, true)]);

        var result = new SquadBuilder(squad, store).Build(Run(false, 3000), [Owned(0, 11)], []);

        Assert.DoesNotContain(log, entry => entry.StartsWith("place-in-app"));
        Assert.Equal(PlacementOutcome.Placed, store.Placements(1)[0].Outcome);
        Assert.Equal(FulfilmentState.Built, result.State);
    }

    [Fact]
    public void APlacementThatDoesNotShowOnTheSquadStopsTheBuild()
    {
        List<string> log = [];
        RecordingStore store = new(log);
        ScriptedSquad squad = new(log, [new SquadSlotView(0, "CB", 0, 0, 0, false),
            new SquadSlotView(1, "CB", 0, 0, 0, false)]) { Refuse = 0 };

        var result = new SquadBuilder(squad, store).Build(Run(false, 3000), [Owned(0, 11), Owned(1, 12)], []);

        Assert.Equal(FulfilmentState.Failed, result.State);
        Assert.Contains("slot 0", result.Detail);
        Assert.Equal(PlacementOutcome.Missing, store.Placements(1)[0].Outcome);
        Assert.Single(store.Placements(1));
    }

    [Fact]
    public void EverySlotFilledLeavesTheRunBuiltAndNothingIsSubmitted()
    {
        List<string> log = [];
        RecordingStore store = new(log);
        ScriptedSquad squad = new(log, [new SquadSlotView(0, "CB", 0, 0, 0, false),
            new SquadSlotView(1, "CB", 0, 0, 0, false)]);

        var result = new SquadBuilder(squad, store).Build(Run(false, 3000), [Owned(0, 11), Owned(1, 12)], []);

        Assert.Equal(FulfilmentState.Built, result.State);
        Assert.Equal(2, log.Count(entry => entry.StartsWith("place-in-app")));
        Assert.All(store.Placements(1), placement => Assert.Equal(PlacementOutcome.Placed, placement.Outcome));
    }

    [Fact]
    public void ABuildWhoseCardsAreNotAllInHandNeverOpensTheChallenge()
    {
        List<string> log = [];
        RecordingStore store = new(log);
        ScriptedSquad squad = new(log, []);

        var result = new SquadBuilder(squad, store).Build(Run(false, 3000), [Bought(0)], [Gap(0)]);

        Assert.Empty(log);
        Assert.Equal(FulfilmentState.Failed, result.State);
        Assert.Contains("slot 0", result.Detail);
    }

    [Fact]
    public void NoAgentTheBuilderDrivesOffersASubmitAction()
    {
        var members = typeof(ISquadAgent).GetMembers().Concat(typeof(IMarketAgent).GetMembers())
            .Concat(typeof(IFulfilmentStore).GetMembers()).Select(member => member.Name);

        Assert.DoesNotContain(members, name => name.Contains("ubmit", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TheGuardRefusesTheControlThatWouldCompleteTheChallenge()
    {
        Assert.Throws<InvalidOperationException>(() => ForbiddenControls.Require("Submit"));
        Assert.Throws<InvalidOperationException>(() => ForbiddenControls.Require("submit squad"));
        Assert.Throws<InvalidOperationException>(() => ForbiddenControls.Require("Exchange"));
        Assert.Throws<InvalidOperationException>(() => ForbiddenControls.Require("List on Transfer Market"));
        Assert.Throws<InvalidOperationException>(() => ForbiddenControls.Require("Quick Sell"));
        Assert.Throws<InvalidOperationException>(() => ForbiddenControls.Require("Discard Player"));
    }

    [Fact]
    public void TheGuardStillLetsThroughTheControlsTheBuildNeeds()
    {
        ForbiddenControls.Require("Add Player");
        ForbiddenControls.Require("Search on Transfer Market");
        ForbiddenControls.Require("Make Bid");
    }

    [Fact]
    public void NoFulfilmentSourceFileReachesForASubmitControl()
    {
        var root = SourceTree.Root();
        var offenders = Directory.GetFiles(Path.Combine(root, "Automation", "Sbc", "Fulfilment"), "*.cs")
            .Where(path => Path.GetFileName(path) != SourceTree.GUARD_FILE)
            .Where(path => File.ReadAllText(path).Contains("ubmit", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFileName).ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void NoFulfilmentSourceFileSellsListsOrDiscardsACard()
    {
        var root = SourceTree.Root();
        string[] banned = ["QuickSell", "Quick Sell", "Discard", "LIST_ITEM", "SEND_TO_TRANSFER_LIST",
            "ListItemsFromTransferList", "ListSelectedItem"];
        var offenders = Directory.GetFiles(Path.Combine(root, "Automation", "Sbc", "Fulfilment"), "*.cs")
            .Where(path => Path.GetFileName(path) != SourceTree.GUARD_FILE)
            .Where(path => banned.Any(word => File.ReadAllText(path).Contains(word, StringComparison.Ordinal)))
            .Select(Path.GetFileName).ToList();

        Assert.Empty(offenders);
    }
}
