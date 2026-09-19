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

    private static SquadSlotView Empty(int slot)
    {
        return new SquadSlotView(slot, "CB", 0, 0, 0, false);
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
    public void ThePlanSeparatesTheCardsInTheClubFromTheOnesStillToBuy()
    {
        var targets = SquadPlan.Targets([Owned(0, 11), Bought(1), Bought(2)], [Won(1, 99), Gap(2)]);

        Assert.Equal([0], SquadPlan.Owned(targets).Select(target => target.SlotIndex));
        Assert.Equal([1], SquadPlan.Bought(targets).Select(target => target.SlotIndex));
        Assert.Equal("slot 2", SquadPlan.Names(SquadPlan.Outstanding(targets)));
    }

    [Fact]
    public void ASimulatedGapCountsAsACardInHandSoADryRunCanFillTheSquad()
    {
        var targets = SquadPlan.Targets([Bought(0)], [Gap(0) with { Outcome = GapOutcome.Simulated }]);

        Assert.Single(SquadPlan.Bought(targets));
        Assert.Empty(SquadPlan.Outstanding(targets));
    }

    [Fact]
    public void ADryRunPlacesNothingAndWritesDownWhatItWouldPlace()
    {
        List<string> log = [];
        RecordingStore store = new(log);
        ScriptedSquad squad = new(log, [Empty(0)]);

        var report = new SquadBuilder(squad, store).PlaceOwned(Run(FulfilmentMode.Dry, 3000), [Owned(0, 11)]);

        Assert.DoesNotContain(log, entry => entry.StartsWith("place-in-app"));
        Assert.Equal(PlacementOutcome.Simulated, store.Placements(1)[0].Outcome);
        Assert.False(report.Halted);
        Assert.Equal(1, report.Placed);
    }

    [Fact]
    public void ASlotAlreadyHoldingTheIntendedCardIsLeftAlone()
    {
        List<string> log = [];
        RecordingStore store = new(log);
        ScriptedSquad squad = new(log, [new SquadSlotView(0, "CB", 11, 0, 70, true)]);

        var report = new SquadBuilder(squad, store).PlaceOwned(Run(FulfilmentMode.Live, 3000), [Owned(0, 11)]);

        Assert.DoesNotContain(log, entry => entry.StartsWith("place-in-app"));
        Assert.Equal(PlacementOutcome.Placed, store.Placements(1)[0].Outcome);
        Assert.False(report.Halted);
    }

    [Fact]
    public void APlacementThatDoesNotShowOnTheSquadStopsTheRun()
    {
        List<string> log = [];
        RecordingStore store = new(log);
        ScriptedSquad squad = new(log, [Empty(0), Empty(1)]) { Refuse = 0 };

        var report = new SquadBuilder(squad, store).PlaceOwned(Run(FulfilmentMode.Live, 3000), [Owned(0, 11), Owned(1, 12)]);

        Assert.True(report.Halted);
        Assert.Contains("slot 0", report.Detail);
        Assert.Equal(PlacementOutcome.Missing, store.Placements(1)[0].Outcome);
        Assert.Single(store.Placements(1));
    }

    [Fact]
    public void EveryClubCardGoesInAndNothingIsSubmitted()
    {
        List<string> log = [];
        RecordingStore store = new(log);
        ScriptedSquad squad = new(log, [Empty(0), Empty(1)]);

        var report = new SquadBuilder(squad, store).PlaceOwned(Run(FulfilmentMode.Live, 3000), [Owned(0, 11), Owned(1, 12)]);

        Assert.False(report.Halted);
        Assert.Equal(2, report.Placed);
        Assert.Equal(2, log.Count(entry => entry.StartsWith("place-in-app")));
        Assert.All(store.Placements(1), placement => Assert.Equal(PlacementOutcome.Placed, placement.Outcome));
    }

    [Fact]
    public void TheClubPassIgnoresTheSlotsThatAreStillToBeBought()
    {
        List<string> log = [];
        RecordingStore store = new(log);
        ScriptedSquad squad = new(log, [Empty(0), Empty(1)]);

        var report = new SquadBuilder(squad, store).PlaceOwned(Run(FulfilmentMode.Live, 3000), [Owned(0, 11), Bought(1)]);

        Assert.Equal(1, report.Placed);
        Assert.Single(store.Placements(1));
        Assert.Equal(0, store.Placements(1)[0].SlotIndex);
    }

    [Fact]
    public void ASlotWhoseCardIsNotYetWonIsLeftAloneAndNeverOpensTheChallenge()
    {
        List<string> log = [];
        RecordingStore store = new(log);
        ScriptedSquad squad = new(log, []);

        var report = new SquadBuilder(squad, store).PlaceBought(Run(FulfilmentMode.Live, 3000), [Bought(0)], [Gap(0)]);

        Assert.Empty(log);
        Assert.False(report.Halted);
        Assert.Equal(0, report.Placed);
    }

    [Fact]
    public void AWonCardIsPlacedOnceTheAuctionIsBanked()
    {
        List<string> log = [];
        RecordingStore store = new(log);
        ScriptedSquad squad = new(log, [Empty(0)]);

        var report = new SquadBuilder(squad, store).PlaceBought(Run(FulfilmentMode.Live, 3000), [Bought(0)], [Won(0, 99)]);

        Assert.Equal(1, report.Placed);
        Assert.Contains("place-in-app 0 99", log);
        Assert.Equal(PlacementOutcome.Placed, store.Placements(1)[0].Outcome);
    }

    [Fact]
    public void ACardAlreadyVerifiedOnTheSquadIsNotPlacedTwice()
    {
        List<string> log = [];
        RecordingStore store = new(log);
        ScriptedSquad squad = new(log, [Empty(0)]);
        SquadBuilder builder = new(squad, store);

        builder.PlaceBought(Run(FulfilmentMode.Live, 3000), [Bought(0)], [Won(0, 99)]);

        var second = builder.PlaceBought(Run(FulfilmentMode.Live, 3000), [Bought(0)], [Won(0, 99)]);

        Assert.Single(log, entry => entry.StartsWith("place-in-app"));
        Assert.Equal(0, second.Placed);
        Assert.False(second.Halted);
    }

    [Fact]
    public void NothingLeftToPlaceNeverOpensTheChallenge()
    {
        List<string> log = [];
        RecordingStore store = new(log);
        ScriptedSquad squad = new(log, []);

        var report = new SquadBuilder(squad, store).PlaceOwned(Run(FulfilmentMode.Live, 3000), [Bought(0)]);

        Assert.Empty(log);
        Assert.False(report.Halted);
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
    public void TheControlThatOpensTheSquadIsTheStartChallengeButton()
    {
        Assert.Equal("Start Challenge", ChallengeEntry.Opening(["Start Challenge"]));
        Assert.Equal("Start Challenge", ChallengeEntry.Opening(["  Start Challenge  "]));
    }

    [Fact]
    public void AChallengeAlreadyUnderWayIsReopenedWithGoToChallenge()
    {
        Assert.Equal("Go to Challenge", ChallengeEntry.Opening(["Go to Challenge"]));
    }

    [Fact]
    public void AChallengeThatOffersNoWayIntoTheSquadNamesNoControl()
    {
        Assert.Equal(string.Empty, ChallengeEntry.Opening([]));
        Assert.Equal(string.Empty, ChallengeEntry.Opening(["Complete"]));
        Assert.Equal(string.Empty, ChallengeEntry.Opening(["Expired"]));
    }

    [Fact]
    public void TheControlThatWouldExchangeThePlayersIsNeverChosen()
    {
        Assert.Equal(string.Empty, ChallengeEntry.Opening(["Exchange Players"]));
        Assert.Equal("Start Challenge", ChallengeEntry.Opening(["Exchange Players", "Start Challenge"]));
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
