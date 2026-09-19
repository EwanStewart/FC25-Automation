using Automation.Sbc;

namespace Automation.Tests;

public class MarketSolveTests
{
    private static readonly string[] POSITIONS =
        ["GK", "RB", "CB", "CB", "LB", "RM", "CM", "CM", "LM", "ST", "ST"];

    private static IReadOnlyList<SquadPlayer> Club()
    {
        return POSITIONS.Select((position, index) =>
            new SquadPlayer(index + 1, index + 1, $"Owned {index}", 70, position, [position], 8, 13, 14, 0, true,
                300, true)).ToList();
    }

    private static SolveOptions Options()
    {
        return new SolveOptions(20000, 11, ChemistryThresholds.Default, TeamLinks.None, 60.0);
    }

    private static ChallengeRequirements Parse(string json)
    {
        return RequirementParser.Parse(json).Single();
    }

    private const string THREE_LEAGUES_TWO_NATIONS = """
        {"challenges":[{"name":"3 Leagues & 2 Nations","challengeId":25,"setId":10,"formation":"f343","elgReq":[
          {"type":"LEAGUE_COUNT","eligibilitySlot":1,"eligibilityKey":8,"eligibilityValue":3},
          {"type":"SCOPE","eligibilitySlot":1,"eligibilityKey":13,"eligibilityValue":2},
          {"type":"NATION_COUNT","eligibilitySlot":2,"eligibilityKey":7,"eligibilityValue":2},
          {"type":"SCOPE","eligibilitySlot":2,"eligibilityKey":13,"eligibilityValue":2},
          {"type":"SAME_LEAGUE_COUNT","eligibilitySlot":3,"eligibilityKey":5,"eligibilityValue":6},
          {"type":"SCOPE","eligibilitySlot":3,"eligibilityKey":13,"eligibilityValue":1},
          {"type":"SAME_NATION_COUNT","eligibilitySlot":4,"eligibilityKey":4,"eligibilityValue":6},
          {"type":"SCOPE","eligibilitySlot":4,"eligibilityKey":13,"eligibilityValue":1},
          {"type":"PLAYER_QUALITY","eligibilitySlot":5,"eligibilityKey":3,"eligibilityValue":3},
          {"type":"SCOPE","eligibilitySlot":5,"eligibilityKey":13,"eligibilityValue":2},
          {"type":"CHEMISTRY_POINTS","eligibilitySlot":6,"eligibilityKey":35,"eligibilityValue":30},
          {"type":"SCOPE","eligibilitySlot":6,"eligibilityKey":13,"eligibilityValue":0}
        ],"elgOperation":"AND","type":"OPEN_CHALLENGE"}]}
        """;

    private const string FOUR_LEAGUES_FIVE_NATIONS = """
        {"challenges":[{"name":"4 Leagues & 5 Nations","challengeId":26,"setId":10,"formation":"f4141","elgReq":[
          {"type":"LEAGUE_COUNT","eligibilitySlot":1,"eligibilityKey":8,"eligibilityValue":4},
          {"type":"SCOPE","eligibilitySlot":1,"eligibilityKey":13,"eligibilityValue":2},
          {"type":"NATION_COUNT","eligibilitySlot":2,"eligibilityKey":7,"eligibilityValue":5},
          {"type":"SCOPE","eligibilitySlot":2,"eligibilityKey":13,"eligibilityValue":2},
          {"type":"SAME_LEAGUE_COUNT","eligibilitySlot":3,"eligibilityKey":5,"eligibilityValue":4},
          {"type":"SCOPE","eligibilitySlot":3,"eligibilityKey":13,"eligibilityValue":1},
          {"type":"SAME_NATION_COUNT","eligibilitySlot":4,"eligibilityKey":4,"eligibilityValue":3},
          {"type":"SCOPE","eligibilitySlot":4,"eligibilityKey":13,"eligibilityValue":1},
          {"type":"TEAM_RATING_1_TO_100","eligibilitySlot":5,"eligibilityKey":19,"eligibilityValue":78},
          {"type":"SCOPE","eligibilitySlot":5,"eligibilityKey":13,"eligibilityValue":0},
          {"type":"CHEMISTRY_POINTS","eligibilitySlot":6,"eligibilityKey":35,"eligibilityValue":25},
          {"type":"SCOPE","eligibilitySlot":6,"eligibilityKey":13,"eligibilityValue":0}
        ],"elgOperation":"AND","type":"OPEN_CHALLENGE"}]}
        """;

    private const string ANFIELD_AMBITION = """
        {"challenges":[{"name":"Anfield Ambition","challengeId":30,"setId":13,"formation":"f433","elgReq":[
          {"type":"SAME_LEAGUE_COUNT","eligibilitySlot":1,"eligibilityKey":5,"eligibilityValue":4},
          {"type":"SCOPE","eligibilitySlot":1,"eligibilityKey":13,"eligibilityValue":0},
          {"type":"NATION_COUNT","eligibilitySlot":2,"eligibilityKey":7,"eligibilityValue":2},
          {"type":"SCOPE","eligibilitySlot":2,"eligibilityKey":13,"eligibilityValue":0},
          {"type":"CLUB_COUNT","eligibilitySlot":3,"eligibilityKey":9,"eligibilityValue":3},
          {"type":"SCOPE","eligibilitySlot":3,"eligibilityKey":13,"eligibilityValue":1},
          {"type":"PLAYER_QUALITY","eligibilitySlot":4,"eligibilityKey":3,"eligibilityValue":1},
          {"type":"SCOPE","eligibilitySlot":4,"eligibilityKey":13,"eligibilityValue":0},
          {"type":"CHEMISTRY_POINTS","eligibilitySlot":5,"eligibilityKey":35,"eligibilityValue":8},
          {"type":"SCOPE","eligibilitySlot":5,"eligibilityKey":13,"eligibilityValue":0}
        ],"elgOperation":"AND","type":"OPEN_CHALLENGE"}]}
        """;

    [Fact]
    public void ThreeLeaguesAndTwoNationsIsSolvedByBuying()
    {
        var result = SquadSolver.Solve(Parse(THREE_LEAGUES_TWO_NATIONS), Club(), Options());

        Assert.Equal(SolveOutcome.Solved, result.Outcome);
        Assert.True(result.Assessment!.IsValid);
        Assert.Equal(3, result.Slots.Select(slot => slot.Player.LeagueId).Distinct().Count());
        Assert.Equal(2, result.Slots.Select(slot => slot.Player.NationId).Distinct().Count());
    }

    [Fact]
    public void FourLeaguesAndFiveNationsIsSolvedByBuying()
    {
        var result = SquadSolver.Solve(Parse(FOUR_LEAGUES_FIVE_NATIONS), Club(), Options());

        Assert.Equal(SolveOutcome.Solved, result.Outcome);
        Assert.True(result.Assessment!.IsValid);
        Assert.Equal(4, result.Slots.Select(slot => slot.Player.LeagueId).Distinct().Count());
        Assert.Equal(5, result.Slots.Select(slot => slot.Player.NationId).Distinct().Count());
    }

    [Fact]
    public void AnfieldAmbitionIsSolvedInsideThreeClubs()
    {
        var result = SquadSolver.Solve(Parse(ANFIELD_AMBITION), Club(), Options());

        Assert.Equal(SolveOutcome.Solved, result.Outcome);
        Assert.True(result.Assessment!.IsValid);
        Assert.True(result.Slots.Select(slot => slot.Player.TeamId).Distinct().Count() <= 3);
        Assert.True(result.Slots.Select(slot => slot.Player.NationId).Distinct().Count() >= 2);
    }

    [Fact]
    public void EveryBoughtCardCarriesTheLeagueItsClubPlaysIn()
    {
        var result = SquadSolver.Solve(Parse(ANFIELD_AMBITION), Club(), Options());

        Assert.All(result.Slots.Where(slot => slot.Gap is not null), slot =>
            Assert.Equal(MarketReference.Ea.LeagueOf(slot.Player.TeamId), slot.Player.LeagueId));
    }

    [Fact]
    public void EveryBoughtCardNamesALeagueAndNationTheGameShips()
    {
        var result = SquadSolver.Solve(Parse(FOUR_LEAGUES_FIVE_NATIONS), Club(), Options());

        Assert.All(result.Slots.Where(slot => slot.Gap is not null), slot =>
        {
            Assert.Contains(slot.Player.LeagueId, MarketReference.Ea.Leagues);
            Assert.True(MarketReference.Ea.KnowsNation(slot.Player.NationId));
        });
    }

    [Fact]
    public void EveryPurchaseSaysWhetherItsCombinationHasBeenSeen()
    {
        var result = SquadSolver.Solve(Parse(ANFIELD_AMBITION), Club(), Options());
        var gaps = result.Slots.Where(slot => slot.Gap is not null).Select(slot => slot.Gap!).ToList();

        Assert.NotEmpty(gaps);
        Assert.All(gaps.Where(gap => gap.Evidence == MarketEvidence.Unverified),
            gap => Assert.Contains("unverified", gap.Describe()));
    }

    [Fact]
    public void ALeagueTheGameDoesNotShipIsStillUnsolvable()
    {
        const string json = """
            {"challenges":[{"name":"Fantasy League","challengeId":99,"setId":1,"formation":"f442","elgReq":[
              {"type":"PLAYER_COUNT","eligibilitySlot":1,"eligibilityKey":2,"eligibilityValue":1},
              {"type":"SCOPE","eligibilitySlot":1,"eligibilityKey":13,"eligibilityValue":0},
              {"type":"LEAGUE_ID","eligibilitySlot":1,"eligibilityKey":11,"eligibilityValue":987654}
            ],"elgOperation":"AND","type":"OPEN_CHALLENGE"}]}
            """;

        Assert.Equal(SolveOutcome.Unsatisfiable, SquadSolver.Solve(Parse(json), Club(), Options()).Outcome);
    }

    [Fact]
    public void ANationTheGameDoesNotShipIsStillUnsolvable()
    {
        const string json = """
            {"challenges":[{"name":"Fantasy Nation","challengeId":98,"setId":1,"formation":"f442","elgReq":[
              {"type":"PLAYER_COUNT","eligibilitySlot":1,"eligibilityKey":2,"eligibilityValue":1},
              {"type":"SCOPE","eligibilitySlot":1,"eligibilityKey":13,"eligibilityValue":0},
              {"type":"NATION_ID","eligibilitySlot":1,"eligibilityKey":10,"eligibilityValue":987654}
            ],"elgOperation":"AND","type":"OPEN_CHALLENGE"}]}
            """;

        Assert.Equal(SolveOutcome.Unsatisfiable, SquadSolver.Solve(Parse(json), Club(), Options()).Outcome);
    }

    [Fact]
    public void AWholeSquadFromOneClubAcrossThreeLeaguesIsStillUnsolvable()
    {
        const string json = """
            {"challenges":[{"name":"Impossible","challengeId":97,"setId":1,"formation":"f442","elgReq":[
              {"type":"SAME_CLUB_COUNT","eligibilitySlot":1,"eligibilityKey":6,"eligibilityValue":11},
              {"type":"SCOPE","eligibilitySlot":1,"eligibilityKey":13,"eligibilityValue":0},
              {"type":"LEAGUE_COUNT","eligibilitySlot":2,"eligibilityKey":8,"eligibilityValue":3},
              {"type":"SCOPE","eligibilitySlot":2,"eligibilityKey":13,"eligibilityValue":2}
            ],"elgOperation":"AND","type":"OPEN_CHALLENGE"}]}
            """;

        Assert.Equal(SolveOutcome.Unsatisfiable, SquadSolver.Solve(Parse(json), Club(), Options()).Outcome);
    }
}
