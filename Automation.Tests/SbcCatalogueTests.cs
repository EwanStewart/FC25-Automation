using Automation.Sbc;
using Automation.Trading;

namespace Automation.Tests;

public class SbcCatalogueTests
{
    private const string EMPTY_SETS = """{"categories":[]}""";
    private const string EMPTY_CHALLENGES = """{"challenges":[]}""";
    private const string MALFORMED = "{not json at all";

    private const string UNKNOWN_FIELDS = """
        {"categories":[{"categoryId":9,"name":"Live","priority":1,"newField":{"a":1},"sets":[
          {"setId":77,"name":"Weekly","description":"","challengesCount":2,"challengesCompletedCount":1,
           "endTime":123,"priority":4,"repeatable":true,"timesCompleted":3,"surpriseField":[1,2,3]}]}]}
        """;

    private const string UNKNOWN_CHALLENGE_FIELDS = """
        {"challenges":[{"name":"Odd One","challengeId":99,"setId":77,"formation":"f442","status":"NOT_STARTED",
          "elgReq":[{"type":"PLAYER_COUNT","eligibilitySlot":0,"eligibilityKey":2,"eligibilityValue":11}],
          "newlyAddedByEa":{"nested":true},"elgOperation":"AND"}]}
        """;

    [Fact]
    public void ClassifiesTheSetListCall()
    {
        var kind = UtasPayloads.Classify("GET",
            "https://utas.mob.v1.prd.futc-ext.gcp.ea.com/ut/game/fc27/sbs/sets");

        Assert.Equal(CaptureKind.SbcSets, kind);
    }

    [Fact]
    public void ClassifiesTheChallengeListCall()
    {
        var kind = UtasPayloads.Classify("GET",
            "https://utas.mob.v1.prd.futc-ext.gcp.ea.com/ut/game/fc27/sbs/setId/13/challenges");

        Assert.Equal(CaptureKind.SbcChallenges, kind);
    }

    [Fact]
    public void LeavesTheChallengeSquadCallAlone()
    {
        var kind = UtasPayloads.Classify("POST",
            "https://utas.mob.v1.prd.futc-ext.gcp.ea.com/ut/game/fc27/sbs/challenge/42");

        Assert.Equal(CaptureKind.Other, kind);
    }

    [Fact]
    public void ReadsEverySetFromTheLivePayload()
    {
        var sets = SbcCatalogue.ParseSets(SbcFixture.Read("sbc_sets.json"));

        Assert.Equal(8, sets.Count);
        Assert.Equal(new[] { 1, 3, 4, 5, 6, 10, 13, 16 }, sets.Select(entry => entry.SetId).Order());
    }

    [Fact]
    public void ReadsTheSetHeader()
    {
        var set = SbcCatalogue.ParseSets(SbcFixture.Read("sbc_sets.json")).Single(entry => entry.SetId == 1);

        Assert.Equal("Intro to SBCs", set.Name);
        Assert.Equal(5, set.CategoryId);
        Assert.Equal("Foundations", set.CategoryName);
        Assert.Equal(4, set.ChallengesCount);
        Assert.Equal(4, set.ChallengesCompletedCount);
        Assert.Equal(281440165, set.EndTime);
        Assert.True(set.Completed);
    }

    [Fact]
    public void MarksAnUnfinishedSetIncomplete()
    {
        var set = SbcCatalogue.ParseSets(SbcFixture.Read("sbc_sets.json")).Single(entry => entry.SetId == 3);

        Assert.Equal(0, set.ChallengesCompletedCount);
        Assert.False(set.Completed);
    }

    [Fact]
    public void ReadsTheChallengeHeader()
    {
        var challenge = SbcCatalogue.ParseChallenges(SbcFixture.Read("sbc_challenges_13.json")).Single();

        Assert.Equal(30, challenge.ChallengeId);
        Assert.Equal(13, challenge.SetId);
        Assert.Equal("Anfield Ambition", challenge.Name);
        Assert.Equal("f433", challenge.Formation);
        Assert.Equal("IN_PROGRESS", challenge.Status);
        Assert.Equal("CUSTOM_BRICK_CHALLENGE", challenge.Type);
        Assert.Equal("AND", challenge.ElgOperation);
        Assert.Equal(23504, challenge.ContentId);
        Assert.Equal("300000-11f49783-bf31", challenge.ChallengeImageId);
        Assert.False(challenge.Repeatable);
        Assert.Equal(0, challenge.TimesCompleted);
    }

    [Fact]
    public void KeepsTheRawEligibilityJson()
    {
        var challenge = SbcCatalogue.ParseChallenges(SbcFixture.Read("sbc_challenges_13.json")).Single();
        var entries = RequirementParser.Parse(challenge.Body).Single();

        Assert.Contains("SAME_LEAGUE_COUNT", challenge.Eligibility);
        Assert.Contains("CHEMISTRY_POINTS", challenge.Eligibility);
        Assert.Equal(30, entries.ChallengeId);
        Assert.NotEmpty(entries.Requirements);
    }

    [Fact]
    public void ReadsEveryChallengeInAMultiChallengeSet()
    {
        var challenges = SbcCatalogue.ParseChallenges(SbcFixture.Read("sbc_challenges_10.json"));

        Assert.Equal(3, challenges.Count);
        Assert.Equal(new[] { 25, 26, 27 }, challenges.Select(entry => entry.ChallengeId).Order());
        Assert.All(challenges, challenge => Assert.Equal(10, challenge.SetId));
    }

    [Fact]
    public void TakesEmptyPayloadsQuietly()
    {
        Assert.Empty(SbcCatalogue.ParseSets(EMPTY_SETS));
        Assert.Empty(SbcCatalogue.ParseChallenges(EMPTY_CHALLENGES));
        Assert.Empty(SbcCatalogue.ParseSets(string.Empty));
        Assert.Empty(SbcCatalogue.ParseChallenges(string.Empty));
    }

    [Fact]
    public void TakesMalformedPayloadsQuietly()
    {
        Assert.Empty(SbcCatalogue.ParseSets(MALFORMED));
        Assert.Empty(SbcCatalogue.ParseChallenges(MALFORMED));
    }

    [Fact]
    public void IgnoresFieldsItDoesNotKnow()
    {
        var set = SbcCatalogue.ParseSets(UNKNOWN_FIELDS).Single();
        var challenge = SbcCatalogue.ParseChallenges(UNKNOWN_CHALLENGE_FIELDS).Single();

        Assert.Equal(77, set.SetId);
        Assert.Equal("Weekly", set.Name);
        Assert.True(set.Repeatable);
        Assert.Equal(3, set.TimesCompleted);
        Assert.Equal(99, challenge.ChallengeId);
        Assert.Equal("Odd One", challenge.Name);
    }

    [Fact]
    public void ReportsNothingObservedWhenNoResponseArrived()
    {
        var reading = SbcCatalogue.Read([], []);

        Assert.Equal(SbcCaptureOutcome.NotObserved, reading.Outcome);
        Assert.Empty(reading.Sets);
        Assert.Empty(reading.Challenges);
    }

    [Fact]
    public void ReportsNothingObservedWhenEveryBodyWasEmpty()
    {
        var reading = SbcCatalogue.Read([new SbcResponse(200, "/sbs/sets", string.Empty)], []);

        Assert.Equal(SbcCaptureOutcome.NotObserved, reading.Outcome);
    }

    [Fact]
    public void ReportsAFailureWhenTheSetCallFailed()
    {
        var reading = SbcCatalogue.Read([new SbcResponse(401, "/sbs/sets", "{}")], []);

        Assert.Equal(SbcCaptureOutcome.Failed, reading.Outcome);
        Assert.Contains("401", reading.Detail);
    }

    [Fact]
    public void ReportsNoItemsWhenTheSetListCameBackEmpty()
    {
        var reading = SbcCatalogue.Read([new SbcResponse(200, "/sbs/sets", EMPTY_SETS)], []);

        Assert.Equal(SbcCaptureOutcome.NoItems, reading.Outcome);
    }

    [Fact]
    public void MergesSetsAndChallengesFromEveryResponse()
    {
        var responses = new[] { new SbcResponse(200, "/sbs/sets", SbcFixture.Read("sbc_sets.json")) };
        var challenges = new[]
        {
            new SbcResponse(200, "/sbs/setId/13/challenges", SbcFixture.Read("sbc_challenges_13.json")),
            new SbcResponse(200, "/sbs/setId/10/challenges", SbcFixture.Read("sbc_challenges_10.json"))
        };
        var reading = SbcCatalogue.Read(responses, challenges);

        Assert.Equal(SbcCaptureOutcome.Captured, reading.Outcome);
        Assert.Equal(8, reading.Sets.Count);
        Assert.Equal(4, reading.Challenges.Count);
        Assert.Contains("4 challenges", reading.Detail);
    }

    [Fact]
    public void KeepsOneCopyOfAChallengeSeenTwice()
    {
        var body = SbcFixture.Read("sbc_challenges_5.json");
        var reading = SbcCatalogue.Read([new SbcResponse(200, "/sbs/sets", SbcFixture.Read("sbc_sets.json"))],
        [
            new SbcResponse(200, "/sbs/setId/5/challenges", body),
            new SbcResponse(200, "/sbs/setId/5/challenges", body)
        ]);

        Assert.Single(reading.Challenges);
    }

    [Fact]
    public void NamesTheSetsWhoseChallengesNeverArrived()
    {
        var reading = SbcCatalogue.Read([new SbcResponse(200, "/sbs/sets", SbcFixture.Read("sbc_sets.json"))],
            [new SbcResponse(200, "/sbs/setId/13/challenges", SbcFixture.Read("sbc_challenges_13.json"))]);

        Assert.Equal(new[] { 3, 4, 5, 6, 10, 16 }, reading.MissingSets.Order());
    }

    [Fact]
    public void DoesNotCountACompletedSetAsMissing()
    {
        var reading = SbcCatalogue.Read([new SbcResponse(200, "/sbs/sets", SbcFixture.Read("sbc_sets.json"))], []);

        Assert.DoesNotContain(1, reading.MissingSets);
    }
}
