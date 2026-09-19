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
}
