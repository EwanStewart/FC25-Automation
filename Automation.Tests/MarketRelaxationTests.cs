using Automation.Sbc;

namespace Automation.Tests;

public class MarketRelaxationTests
{
    private static readonly string[] POSITIONS =
        ["GK", "RB", "CB", "CB", "LB", "RM", "CM", "CM", "LM", "ST", "ST"];

    private const string CELTIC_V_RANGERS = """
        {"challenges":[{"name":"Celtic v Rangers","challengeId":35,"setId":16,"formation":"f442","elgReq":[
          {"type":"PLAYER_COUNT","eligibilitySlot":1,"eligibilityKey":2,"eligibilityValue":1},
          {"type":"SCOPE","eligibilitySlot":1,"eligibilityKey":13,"eligibilityValue":0},
          {"type":"NATION_ID","eligibilitySlot":1,"eligibilityKey":10,"eligibilityValue":42},
          {"type":"CLUB_COUNT","eligibilitySlot":2,"eligibilityKey":9,"eligibilityValue":2},
          {"type":"SCOPE","eligibilitySlot":2,"eligibilityKey":13,"eligibilityValue":0},
          {"type":"PLAYER_COUNT","eligibilitySlot":3,"eligibilityKey":2,"eligibilityValue":1},
          {"type":"SCOPE","eligibilitySlot":3,"eligibilityKey":13,"eligibilityValue":0},
          {"type":"PLAYER_LEVEL","eligibilitySlot":3,"eligibilityKey":17,"eligibilityValue":2},
          {"type":"PLAYER_QUALITY","eligibilitySlot":4,"eligibilityKey":3,"eligibilityValue":1},
          {"type":"SCOPE","eligibilitySlot":4,"eligibilityKey":13,"eligibilityValue":0},
          {"type":"CHEMISTRY_POINTS","eligibilitySlot":5,"eligibilityKey":35,"eligibilityValue":0},
          {"type":"SCOPE","eligibilitySlot":5,"eligibilityKey":13,"eligibilityValue":0}
        ],"elgOperation":"AND","type":"OPEN_CHALLENGE"}]}
        """;

    private const string SCOT_CAPPED_AT_FORTY_FIVE = """
        {"challenges":[{"name":"Capped Scot","challengeId":64,"setId":16,"formation":"f442","elgReq":[
          {"type":"PLAYER_COUNT","eligibilitySlot":1,"eligibilityKey":2,"eligibilityValue":1},
          {"type":"SCOPE","eligibilitySlot":1,"eligibilityKey":13,"eligibilityValue":0},
          {"type":"NATION_ID","eligibilitySlot":1,"eligibilityKey":10,"eligibilityValue":42},
          {"type":"TEAM_RATING_1_TO_100","eligibilitySlot":2,"eligibilityKey":19,"eligibilityValue":45},
          {"type":"SCOPE","eligibilitySlot":2,"eligibilityKey":13,"eligibilityValue":1}
        ],"elgOperation":"AND","type":"OPEN_CHALLENGE"}]}
        """;

    private static ChallengeRequirements Parse(string json)
    {
        return RequirementParser.Parse(json).Single();
    }

    private static SolveOptions Options()
    {
        return new SolveOptions(20000, 11, ChemistryThresholds.Default, TeamLinks.None, 60.0);
    }

    private static IReadOnlyList<SquadPlayer> Club(Func<int, int> team, int rating = 70)
    {
        return POSITIONS.Select((position, index) => new SquadPlayer(index + 1, index + 1, $"Owned {index}",
            rating, position, [position], team(index), 13, 14, 0, true, 300, true)).ToList();
    }

    private static MarketSpecification Bought(SolvedSquad squad)
    {
        return squad.Slots.Where(slot => slot.Gap is not null).Select(slot => slot.Gap!).Single();
    }

    [Fact]
    public void TheScottishGapAsksOnlyForTheNationTheChallengeNames()
    {
        var squad = SquadSolver.Solve(Parse(CELTIC_V_RANGERS), Club(index => 100 + index), Options());
        var gap = Bought(squad);

        Assert.Equal(42, gap.NationId);
        Assert.Null(gap.ClubId);
        Assert.Null(gap.LeagueId);
    }

    [Fact]
    public void TheScottishGapTakesAnyRatingItsQualityBandAllows()
    {
        var gap = Bought(SquadSolver.Solve(Parse(CELTIC_V_RANGERS), Club(index => 100 + index), Options()));

        Assert.True(gap.MaximumRating > gap.MinimumRating);
        Assert.Equal(MarketPricing.Floor(gap.Quality), gap.MinimumRating);
        Assert.Equal(MarketPricing.Ceiling(gap.Quality), gap.MaximumRating);
    }

    [Fact]
    public void TheClubPinStaysWhenTheOwnedPlayersShareASingleClub()
    {
        var gap = Bought(SquadSolver.Solve(Parse(CELTIC_V_RANGERS), Club(_ => 8), Options()));

        Assert.NotNull(gap.ClubId);
        Assert.NotEqual(8, gap.ClubId);
    }

    [Fact]
    public void ARatingBandStopsWhereTheSquadRatingWouldBreak()
    {
        var squad = SquadSolver.Solve(Parse(SCOT_CAPPED_AT_FORTY_FIVE), Club(index => 100 + index, 45), Options());
        var gap = Bought(squad);

        Assert.Equal(MarketPricing.Floor(gap.Quality), gap.MinimumRating);
        Assert.True(gap.MaximumRating < MarketPricing.Ceiling(gap.Quality));
    }

    [Fact]
    public void ARelaxedGapCostsLessThanTheCardTheSolverPlanned()
    {
        var gap = Bought(SquadSolver.Solve(Parse(CELTIC_V_RANGERS), Club(index => 100 + index), Options()));

        Assert.Equal(MarketPricing.Estimate(gap.Quality, gap.MinimumRating, false, true, false), gap.EstimatedCost);
    }
}
