using Automation.Sbc;

namespace Automation.Tests;

public class SquadOutlookTests
{
    private static readonly string[] POSITIONS =
        ["GK", "RB", "CB", "CB", "LB", "RM", "CM", "CM", "LM", "ST", "ST"];

    private const string TWO_CLUBS = """
        {"challenges":[{"name":"Two Clubs","challengeId":60,"setId":1,"formation":"f442","elgReq":[
          {"type":"CLUB_COUNT","eligibilitySlot":1,"eligibilityKey":9,"eligibilityValue":2},
          {"type":"SCOPE","eligibilitySlot":1,"eligibilityKey":13,"eligibilityValue":0}
        ],"elgOperation":"AND","type":"OPEN_CHALLENGE"}]}
        """;

    private const string THREE_CLUBS_AT_MOST = """
        {"challenges":[{"name":"At Most Three Clubs","challengeId":61,"setId":1,"formation":"f442","elgReq":[
          {"type":"CLUB_COUNT","eligibilitySlot":1,"eligibilityKey":9,"eligibilityValue":3},
          {"type":"SCOPE","eligibilitySlot":1,"eligibilityKey":13,"eligibilityValue":1}
        ],"elgOperation":"AND","type":"OPEN_CHALLENGE"}]}
        """;

    private const string ONE_SCOT = """
        {"challenges":[{"name":"One Scot","challengeId":62,"setId":1,"formation":"f442","elgReq":[
          {"type":"PLAYER_COUNT","eligibilitySlot":1,"eligibilityKey":2,"eligibilityValue":1},
          {"type":"SCOPE","eligibilitySlot":1,"eligibilityKey":13,"eligibilityValue":0},
          {"type":"NATION_ID","eligibilitySlot":1,"eligibilityKey":10,"eligibilityValue":42}
        ],"elgOperation":"AND","type":"OPEN_CHALLENGE"}]}
        """;

    private const string RATED_SEVENTY = """
        {"challenges":[{"name":"Rated Seventy","challengeId":63,"setId":1,"formation":"f442","elgReq":[
          {"type":"TEAM_RATING_1_TO_100","eligibilitySlot":1,"eligibilityKey":19,"eligibilityValue":70},
          {"type":"SCOPE","eligibilitySlot":1,"eligibilityKey":13,"eligibilityValue":0}
        ],"elgOperation":"AND","type":"OPEN_CHALLENGE"}]}
        """;

    private static ChallengeRequirements Parse(string json)
    {
        return RequirementParser.Parse(json).Single();
    }

    private static IReadOnlyList<SquadPlayer> Squad(Func<int, int> club, Func<int, int> nation, int rating = 70)
    {
        return POSITIONS.Select((position, index) => new SquadPlayer(index + 1, index + 1, $"Player {index}",
            rating, position, [position], club(index), 13, nation(index), 0, false, 300, true)).ToList();
    }

    private static GapPossibility Anything(int slot)
    {
        return new GapPossibility(slot, null, null, null, null, 45, 64);
    }

    private static IReadOnlyList<SquadRequirement> Breaches(ChallengeRequirements challenge,
        IReadOnlyList<SquadPlayer> squad, IReadOnlyList<GapPossibility> gaps)
    {
        return SquadOutlook.Breaches(challenge, squad, gaps, ChemistryThresholds.Default, TeamLinks.None);
    }

    [Fact]
    public void AFreeClubKeepsAMinimumClubCountTheOwnedPlayersAlreadyReach()
    {
        var breaches = Breaches(Parse(TWO_CLUBS), Squad(index => index + 1, _ => 45), [Anything(0)]);

        Assert.Empty(breaches);
    }

    [Fact]
    public void AFreeClubBreaksAMinimumClubCountOnlyTheBoughtCardCouldReach()
    {
        var breaches = Breaches(Parse(TWO_CLUBS), Squad(_ => 5, _ => 45), [Anything(0)]);

        Assert.Single(breaches);
        Assert.Equal(RequirementKind.DistinctClubs, breaches[0].Kind);
    }

    [Fact]
    public void APinnedClubHoldsAMinimumClubCountTheOwnedPlayersMiss()
    {
        var gap = Anything(0) with { ClubId = 9 };

        Assert.Empty(Breaches(Parse(TWO_CLUBS), Squad(_ => 5, _ => 45), [gap]));
    }

    [Fact]
    public void AFreeClubBreaksAMaximumClubCountAlreadyAtItsLimit()
    {
        var squad = Squad(index => index % 3, _ => 45);

        Assert.Single(Breaches(Parse(THREE_CLUBS_AT_MOST), squad, [Anything(0)]));
    }

    [Fact]
    public void AFreeNationBreaksTheIdentityRequirementOnlyTheBoughtCardSatisfies()
    {
        var breaches = Breaches(Parse(ONE_SCOT), Squad(index => index + 1, _ => 45), [Anything(0)]);

        Assert.Single(breaches);
        Assert.Equal(RequirementKind.PlayerCount, breaches[0].Kind);
    }

    [Fact]
    public void APinnedNationHoldsTheIdentityRequirement()
    {
        var gap = Anything(0) with { NationId = 42 };

        Assert.Empty(Breaches(Parse(ONE_SCOT), Squad(index => index + 1, _ => 45), [gap]));
    }

    [Fact]
    public void AWideRatingBandBreaksASquadRatingItsLowEndCannotHold()
    {
        Assert.Single(Breaches(Parse(RATED_SEVENTY), Squad(index => index + 1, _ => 45), [Anything(0)]));
    }

    [Fact]
    public void ARatingBandWhoseLowEndStillCarriesTheSquadHoldsTheSquadRating()
    {
        var gap = Anything(0) with { MinimumRating = 63, MaximumRating = 64 };

        Assert.Empty(Breaches(Parse(RATED_SEVENTY), Squad(index => index + 1, _ => 45, 72), [gap]));
    }

    [Fact]
    public void ASquadWithNoGapsReportsExactlyWhatTheAssessorFails()
    {
        var squad = Squad(_ => 5, _ => 45);
        var challenge = Parse(TWO_CLUBS);
        var assessment = SquadAssessor.Assess(challenge, squad, null, ChemistryThresholds.Default, TeamLinks.None);

        Assert.Equal(assessment.Outcomes.Count(outcome => !outcome.Passed), Breaches(challenge, squad, []).Count);
    }
}
