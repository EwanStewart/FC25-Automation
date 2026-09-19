using Automation.Sbc;

namespace Automation.Tests;

public class SbcSolveSummaryTests
{
    private static ChallengeRequirements Challenge(int id, params SquadRequirement[] requirements)
    {
        return new ChallengeRequirements(id, 1, $"Challenge {id}", "f442", requirements);
    }

    private static SquadRequirement Unsupported(string subject)
    {
        return new SquadRequirement(RequirementKind.Unsupported, RequirementComparison.Minimum, 1, [],
            $"Unsupported requirement {subject}", subject);
    }

    private static SquadRequirement Supported()
    {
        return new SquadRequirement(RequirementKind.SquadRating, RequirementComparison.Minimum, 80, [], "Squad Rating");
    }

    private static DraftedChallenge Drafted(int id, SolveOutcome outcome, int purchases, long cost,
        params SquadRequirement[] requirements)
    {
        return new DraftedChallenge(Challenge(id, requirements),
            new SolvedSquad(outcome, "detail", [], cost, purchases, null));
    }

    private static MarketSpecification Gap(MarketEvidence evidence)
    {
        return new MarketSpecification("ST", PlayerQuality.Gold, 75, 75, 14, 13, null, null, 1200, evidence);
    }

    private static SquadSlot Slot(int index, MarketSpecification? gap)
    {
        var player = new SquadPlayer(index, index, $"Player {index}", 75, "ST", ["ST"], 8, 13, 14, 0, false, 1200,
            gap is null);

        return new SquadSlot(index, "ST", player, gap);
    }

    [Fact]
    public void CountsThePurchasesNothingProvesExist()
    {
        var squad = new SolvedSquad(SolveOutcome.Solved, "detail",
            [Slot(0, null), Slot(1, Gap(MarketEvidence.Observed)), Slot(2, Gap(MarketEvidence.Unverified)),
                Slot(3, Gap(MarketEvidence.Unverified))], 3600, 3, null);

        var summary = SbcSolveSummary.Summarise([new DraftedChallenge(Challenge(1, Supported()), squad)]);

        Assert.Equal(3, summary.Purchases);
        Assert.Equal(2, summary.UnverifiedPurchases);
        Assert.Equal(1, summary.SolvesRestingOnUnverifiedCards);
    }

    [Fact]
    public void CountsNoUnverifiedPurchaseWhenEverythingCameFromTheClub()
    {
        var squad = new SolvedSquad(SolveOutcome.Solved, "detail", [Slot(0, null)], 0, 0, null);

        var summary = SbcSolveSummary.Summarise([new DraftedChallenge(Challenge(1, Supported()), squad)]);

        Assert.Equal(0, summary.UnverifiedPurchases);
        Assert.Equal(0, summary.SolvesRestingOnUnverifiedCards);
    }

    [Fact]
    public void CountsAnEmptyRunAsNothing()
    {
        var summary = SbcSolveSummary.Summarise([]);

        Assert.Equal(0, summary.Challenges);
        Assert.Empty(summary.UnsupportedTypes);
    }

    [Fact]
    public void SeparatesOwnedSolvesFromBoughtSolves()
    {
        var summary = SbcSolveSummary.Summarise([
            Drafted(1, SolveOutcome.Solved, 0, 0, Supported()),
            Drafted(2, SolveOutcome.Solved, 3, 12000, Supported()),
            Drafted(3, SolveOutcome.Solved, 1, 4000, Supported())
        ]);

        Assert.Equal(3, summary.Challenges);
        Assert.Equal(1, summary.SolvedFromOwnedPlayers);
        Assert.Equal(2, summary.SolvedWithPurchases);
        Assert.Equal(16000, summary.PurchaseCost);
        Assert.Equal(4, summary.Purchases);
    }

    [Fact]
    public void CountsUnsolvableAndRefusedSeparately()
    {
        var summary = SbcSolveSummary.Summarise([
            Drafted(1, SolveOutcome.Unsatisfiable, 0, 0, Supported()),
            Drafted(2, SolveOutcome.UnknownFormation, 0, 0, Supported()),
            Drafted(3, SolveOutcome.UnsupportedRequirement, 0, 0, Unsupported("NUM_TROPHY_REQUIRED"))
        ]);

        Assert.Equal(2, summary.Unsolvable);
        Assert.Equal(1, summary.Refused);
        Assert.Equal(0, summary.SolvedFromOwnedPlayers);
    }

    [Fact]
    public void GroupsUnsupportedRequirementTypesWithCounts()
    {
        var summary = SbcSolveSummary.Summarise([
            Drafted(1, SolveOutcome.UnsupportedRequirement, 0, 0, Unsupported("NUM_TROPHY_REQUIRED"),
                Unsupported("NUM_TROPHY_REQUIRED")),
            Drafted(2, SolveOutcome.UnsupportedRequirement, 0, 0, Unsupported("NUM_TROPHY_REQUIRED")),
            Drafted(3, SolveOutcome.UnsupportedRequirement, 0, 0, Unsupported("PLAYER_LOANS"))
        ]);

        Assert.Equal(2, summary.UnsupportedTypes.Count);
        Assert.Equal("NUM_TROPHY_REQUIRED", summary.UnsupportedTypes[0].Type);
        Assert.Equal(2, summary.UnsupportedTypes[0].Challenges);
        Assert.Equal(3, summary.UnsupportedTypes[0].Occurrences);
        Assert.Equal("PLAYER_LOANS", summary.UnsupportedTypes[1].Type);
        Assert.Equal(1, summary.UnsupportedTypes[1].Challenges);
    }

    [Fact]
    public void NamesAnUnsupportedTypeTheParserCouldNotLabel()
    {
        var summary = SbcSolveSummary.Summarise([Drafted(1, SolveOutcome.UnsupportedRequirement, 0, 0,
            new SquadRequirement(RequirementKind.Unsupported, RequirementComparison.Minimum, 1, [], "nameless"))]);

        Assert.Equal("unnamed", summary.UnsupportedTypes.Single().Type);
    }

    [Fact]
    public void CarriesTheTypeStraightFromTheEligibilityEntry()
    {
        var requirement = RequirementParser.Build([new EligibilityEntry("NUM_TROPHY_REQUIRED", 0, 16, 3)]).Single();

        Assert.Equal(RequirementKind.Unsupported, requirement.Kind);
        Assert.Equal("NUM_TROPHY_REQUIRED", requirement.Subject);
    }
}
