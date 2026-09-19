using Automation.Sbc;

namespace Automation.Tests;

public class SbcRequirementParserTests
{
    private const string SILVER_UPGRADE = """
        {"challenges":[{"name":"Silver Upgrade","challengeId":42,"setId":5,"formation":"f41212","repeatable":true,"elgReq":[{"type":"PLAYER_QUALITY","eligibilitySlot":1,"eligibilityKey":3,"eligibilityValue":2},{"type":"SCOPE","eligibilitySlot":1,"eligibilityKey":13,"eligibilityValue":2}],"elgOperation":"AND","type":"OPEN_CHALLENGE"}]}
        """;

    private const string MIXED = """
        {"challenges":[{"name":"Marquee Matchups","challengeId":7,"setId":9,"formation":"f442","elgReq":[
          {"type":"PLAYER_COUNT","eligibilitySlot":0,"eligibilityKey":2,"eligibilityValue":11},
          {"type":"SCOPE","eligibilitySlot":0,"eligibilityKey":13,"eligibilityValue":2},
          {"type":"ALL_PLAYERS_CHEMISTRY_POINTS","eligibilitySlot":1,"eligibilityKey":36,"eligibilityValue":14},
          {"type":"SCOPE","eligibilitySlot":1,"eligibilityKey":13,"eligibilityValue":0},
          {"type":"TEAM_RATING","eligibilitySlot":2,"eligibilityKey":19,"eligibilityValue":82},
          {"type":"SCOPE","eligibilitySlot":2,"eligibilityKey":13,"eligibilityValue":0},
          {"type":"CLUB_COUNT","eligibilitySlot":3,"eligibilityKey":9,"eligibilityValue":2},
          {"type":"SCOPE","eligibilitySlot":3,"eligibilityKey":13,"eligibilityValue":0},
          {"type":"NATION_ID","eligibilitySlot":4,"eligibilityKey":10,"eligibilityValue":42},
          {"type":"PLAYER_COUNT","eligibilitySlot":4,"eligibilityKey":2,"eligibilityValue":1},
          {"type":"SCOPE","eligibilitySlot":4,"eligibilityKey":13,"eligibilityValue":0}
        ],"elgOperation":"AND","type":"OPEN_CHALLENGE"}]}
        """;

    private const string RAW_POWER = """
        {"challenges":[{"name":"Raw Power","challengeId":15,"setId":3,"formation":"f442","elgReq":[
          {"type":"PLAYER_COUNT","eligibilitySlot":1,"eligibilityKey":2,"eligibilityValue":11},
          {"type":"SCOPE","eligibilitySlot":1,"eligibilityKey":13,"eligibilityValue":0},
          {"type":"PLAYER_LEVEL","eligibilitySlot":1,"eligibilityKey":17,"eligibilityValue":3},
          {"type":"TEAM_RATING_1_TO_100","eligibilitySlot":2,"eligibilityKey":19,"eligibilityValue":85},
          {"type":"SCOPE","eligibilitySlot":2,"eligibilityKey":13,"eligibilityValue":1},
          {"type":"CHEMISTRY_POINTS","eligibilitySlot":3,"eligibilityKey":35,"eligibilityValue":10},
          {"type":"SCOPE","eligibilitySlot":3,"eligibilityKey":13,"eligibilityValue":0}
        ],"elgOperation":"AND","type":"OPEN_CHALLENGE"}]}
        """;

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
          {"type":"CHEMISTRY_POINTS","eligibilitySlot":5,"eligibilityKey":35,"eligibilityValue":14},
          {"type":"SCOPE","eligibilitySlot":5,"eligibilityKey":13,"eligibilityValue":0}
        ],"elgOperation":"AND","type":"OPEN_CHALLENGE"}]}
        """;

    private const string EXACT_LEVEL = """
        {"challenges":[{"name":"Exactly Gold","challengeId":90,"setId":3,"formation":"f442","elgReq":[
          {"type":"PLAYER_COUNT","eligibilitySlot":1,"eligibilityKey":2,"eligibilityValue":11},
          {"type":"SCOPE","eligibilitySlot":1,"eligibilityKey":13,"eligibilityValue":2},
          {"type":"PLAYER_LEVEL","eligibilitySlot":1,"eligibilityKey":17,"eligibilityValue":3}
        ],"elgOperation":"AND","type":"OPEN_CHALLENGE"}]}
        """;

    private const string LEVEL_WITHOUT_COUNT = """
        {"challenges":[{"name":"Loose Level","challengeId":91,"setId":3,"formation":"f442","elgReq":[
          {"type":"PLAYER_LEVEL","eligibilitySlot":1,"eligibilityKey":17,"eligibilityValue":2},
          {"type":"SCOPE","eligibilitySlot":1,"eligibilityKey":13,"eligibilityValue":0}
        ],"elgOperation":"AND","type":"OPEN_CHALLENGE"}]}
        """;

    private const string UNKNOWN_KEY = """
        {"challenges":[{"name":"Trophy Hunt","challengeId":8,"setId":9,"formation":"f442","elgReq":[
          {"type":"NUM_TROPHY_REQUIRED","eligibilitySlot":0,"eligibilityKey":16,"eligibilityValue":3},
          {"type":"SCOPE","eligibilitySlot":0,"eligibilityKey":13,"eligibilityValue":0}
        ],"elgOperation":"AND","type":"OPEN_CHALLENGE"}]}
        """;

    [Fact]
    public void ReadsTheChallengeHeader()
    {
        var challenge = RequirementParser.Parse(SILVER_UPGRADE).Single();

        Assert.Equal(42, challenge.ChallengeId);
        Assert.Equal(5, challenge.SetId);
        Assert.Equal("Silver Upgrade", challenge.Name);
        Assert.Equal("f41212", challenge.Formation);
    }

    [Fact]
    public void AnExactQualitySlotBecomesAnEveryPlayerRequirement()
    {
        var challenge = RequirementParser.Parse(SILVER_UPGRADE).Single();
        var requirement = challenge.Requirements.Single();

        Assert.Equal(RequirementKind.EveryPlayer, requirement.Kind);
        Assert.Equal(RequirementComparison.Exact, requirement.Comparison);
        Assert.Equal(PlayerFilterKind.Quality, requirement.Filters.Single().Kind);
        Assert.Equal((int)PlayerQuality.Silver, requirement.Filters.Single().Value);
        Assert.Equal("Player Quality: Exactly Silver", requirement.Description);
    }

    [Fact]
    public void ACountedSlotWithNoFilterIsTheSquadSize()
    {
        var challenge = RequirementParser.Parse(MIXED).Single();

        Assert.Equal(11, challenge.SquadSize);
    }

    [Fact]
    public void SquadLevelKeysBecomeTheirOwnRequirements()
    {
        var challenge = RequirementParser.Parse(MIXED).Single();

        Assert.Contains(challenge.Requirements,
            requirement => requirement.Kind == RequirementKind.TotalChemistry && requirement.Value == 14 &&
                           requirement.Comparison == RequirementComparison.Minimum);
        Assert.Contains(challenge.Requirements,
            requirement => requirement.Kind == RequirementKind.SquadRating && requirement.Value == 82);
        Assert.Contains(challenge.Requirements,
            requirement => requirement.Kind == RequirementKind.DistinctClubs && requirement.Value == 2);
    }

    [Fact]
    public void AFilteredCountBecomesAPlayerCountRequirement()
    {
        var challenge = RequirementParser.Parse(MIXED).Single();
        var requirement = challenge.Requirements.Single(candidate =>
            candidate.Kind == RequirementKind.PlayerCount && candidate.Filters.Count == 1);

        Assert.Equal(1, requirement.Value);
        Assert.Equal(PlayerFilterKind.Nation, requirement.Filters.Single().Kind);
        Assert.Equal(42, requirement.Filters.Single().Value);
    }

    [Fact]
    public void AnUnderstoodChallengeCarriesNoUnsupportedRequirement()
    {
        Assert.True(RequirementParser.Parse(MIXED).Single().IsFullyUnderstood);
    }

    [Fact]
    public void AnUnknownKeyIsRecordedAsUnsupported()
    {
        var challenge = RequirementParser.Parse(UNKNOWN_KEY).Single();

        Assert.False(challenge.IsFullyUnderstood);
        Assert.Equal(RequirementKind.Unsupported, challenge.Requirements.Single().Kind);
        Assert.Contains("16", challenge.Requirements.Single().Description);
    }

    [Fact]
    public void RawPowerAsksForElevenGoldPlayers()
    {
        var challenge = RequirementParser.Parse(RAW_POWER).Single();
        var level = challenge.Requirements.Single(requirement =>
            requirement.Kind == RequirementKind.PlayerLevelCount);

        Assert.Equal(RequirementComparison.Minimum, level.Comparison);
        Assert.Equal(11, level.Value);
        Assert.Equal(PlayerFilterKind.Level, level.Filters.Single().Kind);
        Assert.Equal((int)PlayerQuality.Gold, level.Filters.Single().Value);
        Assert.Equal("Gold: Min. 11 Players", level.Description);
    }

    [Fact]
    public void RawPowerIsFullyUnderstood()
    {
        var challenge = RequirementParser.Parse(RAW_POWER).Single();

        Assert.True(challenge.IsFullyUnderstood);
        Assert.Contains(challenge.Requirements,
            requirement => requirement.Kind == RequirementKind.SquadRating &&
                           requirement.Comparison == RequirementComparison.Maximum && requirement.Value == 85);
    }

    [Fact]
    public void CelticVRangersDecodesSlotForSlot()
    {
        var challenge = RequirementParser.Parse(CELTIC_V_RANGERS).Single();

        Assert.True(challenge.IsFullyUnderstood);
        Assert.Equal([
            "Nation 42: Min. 1 Players", "Clubs in Squad: Min. 2", "Silver: Min. 1 Players",
            "Player Quality: Min. Bronze", "Chemistry Points per Player: Min. 14"
        ], challenge.Requirements.Select(requirement => requirement.Description));
    }

    [Fact]
    public void ASilverCountIsAStandingLevelRequirementNotASquadWideFloor()
    {
        var challenge = RequirementParser.Parse(CELTIC_V_RANGERS).Single();
        var level = challenge.Requirements.Single(requirement =>
            requirement.Kind == RequirementKind.PlayerLevelCount);
        var floor = challenge.Requirements.Single(requirement =>
            requirement.Kind == RequirementKind.EveryPlayer);

        Assert.Equal(1, level.Value);
        Assert.Equal((int)PlayerQuality.Silver, level.Filters.Single().Value);
        Assert.Equal(PlayerFilterKind.Quality, floor.Filters.Single().Kind);
        Assert.Equal((int)PlayerQuality.Bronze, floor.Filters.Single().Value);
    }

    [Fact]
    public void AnExactScopeMakesTheLevelCountExact()
    {
        var challenge = RequirementParser.Parse(EXACT_LEVEL).Single();
        var level = challenge.Requirements.Single();

        Assert.Equal(RequirementKind.PlayerLevelCount, level.Kind);
        Assert.Equal(RequirementComparison.Exact, level.Comparison);
        Assert.Equal(11, level.Value);
        Assert.Equal("Gold: Exactly 11 Players", level.Description);
    }

    [Fact]
    public void ALevelWithNoCountInTheSameSlotIsUnsupported()
    {
        var challenge = RequirementParser.Parse(LEVEL_WITHOUT_COUNT).Single();

        Assert.False(challenge.IsFullyUnderstood);
        Assert.Equal("PLAYER_LEVEL", challenge.Requirements.Single().Subject);
    }
}
