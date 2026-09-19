using Automation.Sbc;

namespace Automation.Tests;

public class SquadSolverTests
{
    private static readonly string[] POSITIONS =
        ["GK", "RB", "CB", "CB", "LB", "RM", "CM", "CM", "LM", "ST", "ST"];

    private static IReadOnlyList<SquadPlayer> Club(int rating, int team = 5, int league = 13, int nation = 14)
    {
        return POSITIONS.Select((position, index) =>
            new SquadPlayer(index + 1, index + 1, $"Owned {index}", rating, position, [position], team, league,
                nation, 0, true, 300, true)).ToList();
    }

    private static ChallengeRequirements Challenge(params SquadRequirement[] requirements)
    {
        return new ChallengeRequirements(42, 5, "Test", "f442", requirements);
    }

    private static SquadRequirement SquadSize(int size)
    {
        return new SquadRequirement(RequirementKind.PlayerCount, RequirementComparison.Exact, size, [],
            $"Number of Players in the Squad: {size}");
    }

    private static SolveOptions Options(int budget = 100000)
    {
        return new SolveOptions(budget, 11, ChemistryThresholds.Default, TeamLinks.None);
    }

    [Fact]
    public void FillsEverySlotFromTheClubWhenItCan()
    {
        var result = SquadSolver.Solve(Challenge(SquadSize(11)), Club(70), Options());

        Assert.Equal(SolveOutcome.Solved, result.Outcome);
        Assert.Equal(11, result.Slots.Count);
        Assert.All(result.Slots, slot => Assert.True(slot.Player.Owned));
        Assert.Equal(0, result.PurchaseCount);
    }

    [Fact]
    public void EveryPlacedPlayerStandsInAPositionItCanPlay()
    {
        var result = SquadSolver.Solve(Challenge(SquadSize(11)), Club(70), Options());

        Assert.All(result.Slots, slot => Assert.Contains(slot.Position, slot.Player.PossiblePositions));
    }

    [Fact]
    public void AnUnsupportedRequirementStopsTheSolver()
    {
        var unsupported = new SquadRequirement(RequirementKind.Unsupported, RequirementComparison.Minimum, 1, [],
            "Unsupported requirement");

        var result = SquadSolver.Solve(Challenge(SquadSize(11), unsupported), Club(70), Options());

        Assert.Equal(SolveOutcome.UnsupportedRequirement, result.Outcome);
    }

    [Fact]
    public void AMissingNationBecomesAnAttributeSpecificationNotAName()
    {
        var needsScotland = new SquadRequirement(RequirementKind.PlayerCount, RequirementComparison.Minimum, 1,
            [new PlayerFilter(PlayerFilterKind.Nation, 42, "Scotland")], "Scotland: Min. 1 Player");

        var result = SquadSolver.Solve(Challenge(SquadSize(11), needsScotland), Club(70), Options());

        Assert.Equal(SolveOutcome.Solved, result.Outcome);
        Assert.Equal(1, result.PurchaseCount);
        var gap = result.Slots.Single(slot => slot.Gap is not null).Gap!;
        Assert.Equal(42, gap.NationId);
        Assert.True(result.EstimatedCost > 0);
    }

    [Fact]
    public void AChallengeIsUnsatisfiableWhenNoPurchaseIsAllowed()
    {
        var needsScotland = new SquadRequirement(RequirementKind.PlayerCount, RequirementComparison.Minimum, 1,
            [new PlayerFilter(PlayerFilterKind.Nation, 42, "Scotland")], "Scotland: Min. 1 Player");

        var result = SquadSolver.Solve(Challenge(SquadSize(11), needsScotland), Club(70), Options(0));

        Assert.Equal(SolveOutcome.Unsatisfiable, result.Outcome);
    }

    [Fact]
    public void TheDraftedSquadPassesEveryRequirementItWasGiven()
    {
        var chemistry = new SquadRequirement(RequirementKind.TotalChemistry, RequirementComparison.Minimum, 33, [],
            "Total Chemistry: Min. 33");

        var result = SquadSolver.Solve(Challenge(SquadSize(11), chemistry), Club(70), Options());

        Assert.Equal(SolveOutcome.Solved, result.Outcome);
        Assert.True(result.Assessment!.IsValid);
        Assert.Equal(33, result.Assessment.Chemistry.Total);
    }

    [Fact]
    public void AnEveryPlayerQualityKeepsHigherCardsOutOfTheSquad()
    {
        var owned = Club(70).Concat(Club(84).Select(player => player with { Id = player.Id + 100 })).ToList();
        var silverOnly = new SquadRequirement(RequirementKind.EveryPlayer, RequirementComparison.Exact, 0,
            [new PlayerFilter(PlayerFilterKind.Quality, (int)PlayerQuality.Silver, "Silver")],
            "Player Quality: Exactly Silver");

        var result = SquadSolver.Solve(Challenge(SquadSize(11), silverOnly), owned, Options());

        Assert.Equal(SolveOutcome.Solved, result.Outcome);
        Assert.All(result.Slots, slot => Assert.Equal(PlayerQuality.Silver, QualityBand.Of(slot.Player.Rating)));
    }

    [Fact]
    public void ASquadRatingFloorPushesTheSolverOntoBetterCards()
    {
        var owned = Club(70).Concat(Club(84).Select(player => player with { Id = player.Id + 100 })).ToList();
        var rating = new SquadRequirement(RequirementKind.SquadRating, RequirementComparison.Minimum, 84, [],
            "Squad Rating: Min. 84");

        var result = SquadSolver.Solve(Challenge(SquadSize(11), rating), owned, Options());

        Assert.Equal(SolveOutcome.Solved, result.Outcome);
        Assert.True(result.Assessment!.Rating >= 84);
    }

    [Fact]
    public void AnUnknownFormationIsReportedNotGuessed()
    {
        var challenge = new ChallengeRequirements(1, 1, "Odd", "f9999", [SquadSize(11)]);

        Assert.Equal(SolveOutcome.UnknownFormation, SquadSolver.Solve(challenge, Club(70), Options()).Outcome);
    }

    private static SquadRequirement TierCount(PlayerQuality tier, int count, RequirementComparison comparison)
    {
        return new SquadRequirement(RequirementKind.PlayerLevelCount, comparison, count,
            [new PlayerFilter(PlayerFilterKind.Level, (int)tier, tier.ToString())],
            $"{tier}: {count} Players");
    }

    [Fact]
    public void RawPowerDraftsElevenGoldCardsUnderARatingCeiling()
    {
        var owned = Club(70).Concat(Club(80).Select(player => player with { Id = player.Id + 100 })).ToList();
        var rating = new SquadRequirement(RequirementKind.SquadRating, RequirementComparison.Maximum, 85, [],
            "Squad Rating: Max. 85");

        var result = SquadSolver.Solve(
            Challenge(SquadSize(11), TierCount(PlayerQuality.Gold, 11, RequirementComparison.Minimum), rating),
            owned, Options());

        Assert.Equal(SolveOutcome.Solved, result.Outcome);
        Assert.All(result.Slots, slot => Assert.Equal(PlayerQuality.Gold, QualityBand.Of(slot.Player.Rating)));
        Assert.Equal(0, result.PurchaseCount);
        Assert.True(result.Assessment!.IsValid);
    }

    [Fact]
    public void ATierTheClubLacksIsBought()
    {
        var result = SquadSolver.Solve(
            Challenge(SquadSize(11), TierCount(PlayerQuality.Gold, 2, RequirementComparison.Minimum)),
            Club(70), Options());

        Assert.Equal(SolveOutcome.Solved, result.Outcome);
        Assert.Equal(2, result.PurchaseCount);
        Assert.Equal(2, result.Slots.Count(slot => QualityBand.Of(slot.Player.Rating) == PlayerQuality.Gold));
        Assert.True(result.Assessment!.IsValid);
    }

    [Fact]
    public void AnExactTierCountAdmitsNoMoreThanItAsksFor()
    {
        var owned = Club(70).Concat(Club(80).Select(player => player with { Id = player.Id + 100 })).ToList();

        var result = SquadSolver.Solve(
            Challenge(SquadSize(11), TierCount(PlayerQuality.Gold, 1, RequirementComparison.Exact)), owned,
            Options());

        Assert.Equal(SolveOutcome.Solved, result.Outcome);
        Assert.Equal(1, result.Slots.Count(slot => QualityBand.Of(slot.Player.Rating) == PlayerQuality.Gold));
    }

    [Fact]
    public void ATierCountStandsBesideASquadWideQualityFloor()
    {
        var owned = Club(70).Concat(Club(60).Select(player => player with { Id = player.Id + 100 })).ToList();
        var floor = new SquadRequirement(RequirementKind.EveryPlayer, RequirementComparison.Minimum, 0,
            [new PlayerFilter(PlayerFilterKind.Quality, (int)PlayerQuality.Silver, "Silver")],
            "Player Quality: Min. Silver");

        var result = SquadSolver.Solve(
            Challenge(SquadSize(11), floor, TierCount(PlayerQuality.Gold, 2, RequirementComparison.Minimum)),
            owned, Options());

        Assert.Equal(SolveOutcome.Solved, result.Outcome);
        Assert.DoesNotContain(result.Slots,
            slot => QualityBand.Of(slot.Player.Rating) == PlayerQuality.Bronze);
        Assert.Equal(2, result.Slots.Count(slot => QualityBand.Of(slot.Player.Rating) == PlayerQuality.Gold));
        Assert.True(result.Assessment!.IsValid);
    }

    [Fact]
    public void AnUnknownEligibilityKeyStillRefusesTheChallenge()
    {
        const string json = """
            {"challenges":[{"name":"Trophy Hunt","challengeId":8,"setId":9,"formation":"f442","elgReq":[
              {"type":"NUM_TROPHY_REQUIRED","eligibilitySlot":0,"eligibilityKey":16,"eligibilityValue":3},
              {"type":"SCOPE","eligibilitySlot":0,"eligibilityKey":13,"eligibilityValue":0}
            ],"elgOperation":"AND","type":"OPEN_CHALLENGE"}]}
            """;
        var challenge = RequirementParser.Parse(json).Single();

        Assert.Equal(SolveOutcome.UnsupportedRequirement, SquadSolver.Solve(challenge, Club(70), Options()).Outcome);
    }
}
