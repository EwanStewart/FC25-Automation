using Automation.Sbc;

namespace Automation.Tests;

public class SquadAssessorTests
{
    private static IReadOnlyList<SquadPlayer> Eleven(int rating)
    {
        return Formations.SlotPositions("f442")
            .Select((position, index) => Player(index, rating, position, 5, 13, 14, 0)).ToList();
    }

    private static SquadPlayer Player(int index, int rating, string position, int team, int league, int nation,
        int rare)
    {
        return new SquadPlayer(index + 1, index + 1, $"Player {index}", rating, position, [position], team, league,
            nation, rare, true, 0, true);
    }

    [Theory]
    [InlineData(50, PlayerQuality.Bronze)]
    [InlineData(64, PlayerQuality.Bronze)]
    [InlineData(65, PlayerQuality.Silver)]
    [InlineData(74, PlayerQuality.Silver)]
    [InlineData(75, PlayerQuality.Gold)]
    [InlineData(91, PlayerQuality.Gold)]
    public void QualityFollowsTheRatingBand(int rating, PlayerQuality quality)
    {
        Assert.Equal(quality, QualityBand.Of(rating));
    }

    [Fact]
    public void ASquadOfEqualRatingsRatesAtThatRating()
    {
        Assert.Equal(80, SquadAssessor.Rating(Eleven(80)));
    }

    [Fact]
    public void OneHighCardLiftsTheSquadRatingAboveTheMean()
    {
        var squad = Eleven(80).ToList();
        squad[10] = Player(10, 92, "ST", 5, 13, 14, 0);

        Assert.Equal(82, SquadAssessor.Rating(squad));
    }

    [Fact]
    public void ASquadSizeRequirementCountsThePlayers()
    {
        var requirement = new SquadRequirement(RequirementKind.PlayerCount, RequirementComparison.Exact, 11, [], "");

        var assessment = Assess(Eleven(80), [requirement]);

        Assert.True(assessment.IsValid);
        Assert.Equal(11, assessment.Outcomes.Single().Actual);
    }

    [Fact]
    public void AQualityCountOnlyCountsMatchingCards()
    {
        var squad = Eleven(80).ToList();
        squad[0] = Player(0, 70, "GK", 5, 13, 14, 0);
        var requirement = new SquadRequirement(RequirementKind.PlayerCount, RequirementComparison.Minimum, 1,
            [new PlayerFilter(PlayerFilterKind.Quality, (int)PlayerQuality.Silver, "Silver")], "");

        var assessment = Assess(squad, [requirement]);

        Assert.True(assessment.IsValid);
        Assert.Equal(1, assessment.Outcomes.Single().Actual);
    }

    [Fact]
    public void AnEveryPlayerQualityFloorFailsOnOneLowCard()
    {
        var squad = Eleven(80).ToList();
        squad[0] = Player(0, 60, "GK", 5, 13, 14, 0);
        var requirement = new SquadRequirement(RequirementKind.EveryPlayer, RequirementComparison.Minimum, 0,
            [new PlayerFilter(PlayerFilterKind.Quality, (int)PlayerQuality.Silver, "Silver")], "");

        Assert.False(Assess(squad, [requirement]).IsValid);
    }

    [Fact]
    public void AnEveryPlayerExactQualityRejectsAHigherCard()
    {
        var squad = Eleven(70).ToList();
        squad[0] = Player(0, 84, "GK", 5, 13, 14, 0);
        var requirement = new SquadRequirement(RequirementKind.EveryPlayer, RequirementComparison.Exact, 0,
            [new PlayerFilter(PlayerFilterKind.Quality, (int)PlayerQuality.Silver, "Silver")], "");

        Assert.False(Assess(squad, [requirement]).IsValid);
    }

    [Fact]
    public void DistinctClubsCountsTheClubsPresent()
    {
        var squad = Eleven(80).ToList();
        squad[0] = Player(0, 80, "GK", 6, 13, 14, 0);
        var requirement = new SquadRequirement(RequirementKind.DistinctClubs, RequirementComparison.Minimum, 2, [],
            "");

        var assessment = Assess(squad, [requirement]);

        Assert.True(assessment.IsValid);
        Assert.Equal(2, assessment.Outcomes.Single().Actual);
    }

    [Fact]
    public void TotalChemistryIsMeasuredAgainstTheCalculator()
    {
        var requirement = new SquadRequirement(RequirementKind.TotalChemistry, RequirementComparison.Minimum, 33, [],
            "");

        var assessment = Assess(Eleven(80), [requirement]);

        Assert.Equal(33, assessment.Chemistry.Total);
        Assert.True(assessment.IsValid);
    }

    [Fact]
    public void AnUnsupportedRequirementNeverPasses()
    {
        var requirement = new SquadRequirement(RequirementKind.Unsupported, RequirementComparison.Minimum, 1, [], "");

        var assessment = Assess(Eleven(80), [requirement]);

        Assert.False(assessment.IsValid);
        Assert.False(assessment.Outcomes.Single().Passed);
    }

    private static SquadAssessment Assess(IReadOnlyList<SquadPlayer> squad,
        IReadOnlyList<SquadRequirement> requirements)
    {
        var challenge = new ChallengeRequirements(1, 1, "Test", "f442", requirements);

        return SquadAssessor.Assess(challenge, squad, null, ChemistryThresholds.Default, TeamLinks.None);
    }
}
