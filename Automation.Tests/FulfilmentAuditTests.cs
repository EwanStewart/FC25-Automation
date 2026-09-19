using Automation.Sbc;
using Automation.Sbc.Fulfilment;
using Automation.Trading;
using static Automation.Tests.FulfilmentFixtures;

namespace Automation.Tests;

public class FulfilmentAuditTests
{
    private static readonly string[] POSITIONS =
        ["GK", "RB", "CB", "CB", "LB", "RM", "CM", "CM", "LM", "ST", "ST"];

    private const int GAP_SLOT = 2;
    private const int PINNED_CLUB = 5;
    private const int WON_ASSET = 4242;

    private const string TWO_CLUBS_AT_MOST = """
        {"challenges":[{"name":"Two Clubs At Most","challengeId":1234,"setId":1,"formation":"f442","elgReq":[
          {"type":"CLUB_COUNT","eligibilitySlot":1,"eligibilityKey":9,"eligibilityValue":2},
          {"type":"SCOPE","eligibilitySlot":1,"eligibilityKey":13,"eligibilityValue":1}
        ],"elgOperation":"AND","type":"OPEN_CHALLENGE"}]}
        """;

    private static IReadOnlyList<SquadPlayer> Owned()
    {
        return POSITIONS.Select((position, index) => new SquadPlayer(index + 1, index + 1, $"Owned {index}", 70,
            position, [position], index % 2 == 0 ? PINNED_CLUB : 6, 13, 14, 0, false, 300, true)).ToList();
    }

    private static SquadCensus Census(CardAttributes? card = null)
    {
        return new SquadCensus(RequirementParser.Parse(TWO_CLUBS_AT_MOST).Single(), Owned(),
            ChemistryThresholds.Default, TeamLinks.None, asset => asset == WON_ASSET ? card : null);
    }

    private static GapRecord Pinned(GapOutcome outcome, int? assetId = null)
    {
        var gap = Gap(GAP_SLOT, 900, outcome);

        return gap with
        {
            Specification = gap.Specification with { ClubId = PINNED_CLUB }, AssetId = assetId,
            Detail = "won at 300 and sent to the club"
        };
    }

    private static (BuyingResult result, RecordingStore store) Buy(SquadCensus? census, GapRecord gap,
        IReadOnlyList<AuctionListing>? listings = null, IReadOnlyList<IReadOnlyList<TradeState>>? polls = null)
    {
        List<string> log = [];
        RecordingStore store = new(log);
        ScriptedMarket market = new(log,
            new Dictionary<int, IReadOnlyList<AuctionListing>> { [0] = listings ?? [] });

        foreach (var poll in polls ?? []) market.Polls.Enqueue(poll);

        return (new GapBuyer(market, store, census).Buy(Run(FulfilmentMode.Live, 100000), [gap]), store);
    }

    [Fact]
    public void ACardWonAtAClubTheSquadCannotCarryIsReported()
    {
        var (result, store) = Buy(Census(new CardAttributes(9, 13, 14, 0, 68)),
            Pinned(GapOutcome.Won, WON_ASSET));

        Assert.Equal(GapOutcome.Mismatched, store.Gaps(1)[0].Outcome);
        Assert.Contains("Clubs in Squad", store.Gaps(1)[0].Detail);
        Assert.Equal(FulfilmentState.Failed, result.State);
    }

    [Fact]
    public void ACardWonAwayFromTheClubItsGapAskedForIsNamed()
    {
        var (_, store) = Buy(Census(new CardAttributes(9, 13, 14, 0, 68)), Pinned(GapOutcome.Won, WON_ASSET));

        Assert.Contains("club 9, not club 5", store.Gaps(1)[0].Detail);
    }

    [Fact]
    public void ACardWonExactlyAsTheGapAskedIsLeftAlone()
    {
        var (result, store) = Buy(Census(new CardAttributes(PINNED_CLUB, 13, 14, 0, 68)),
            Pinned(GapOutcome.Won, WON_ASSET));

        Assert.Equal(GapOutcome.Won, store.Gaps(1)[0].Outcome);
        Assert.NotEqual(FulfilmentState.Failed, result.State);
    }

    [Fact]
    public void AWinNoCatalogueCanDescribeIsLeftAlone()
    {
        var (_, store) = Buy(Census(), Pinned(GapOutcome.Won, WON_ASSET));

        Assert.Equal(GapOutcome.Won, store.Gaps(1)[0].Outcome);
    }

    [Fact]
    public void ARunWithNoCensusChecksNothing()
    {
        var (result, store) = Buy(null, Pinned(GapOutcome.Won, WON_ASSET));

        Assert.Equal(GapOutcome.Won, store.Gaps(1)[0].Outcome);
        Assert.NotEqual(FulfilmentState.Failed, result.State);
    }

    [Fact]
    public void ACardWonThisRunIsCheckedAgainstWhatTheCatalogueSaysItIs()
    {
        var listing = Listing("t1", 300) with { TeamId = PINNED_CLUB, AssetId = WON_ASSET };
        var (result, store) = Buy(Census(new CardAttributes(9, 13, 14, 0, 68)), Pinned(GapOutcome.Pending),
            [listing], [[Due("t1", 300)], [Ended("t1", 300, "highest")]]);

        Assert.Equal(GapOutcome.Mismatched, store.Gaps(1)[0].Outcome);
        Assert.Equal(FulfilmentState.Failed, result.State);
    }

    [Fact]
    public void TheCensusReadsTheApprovalInSlotOrder()
    {
        var club = Owned();
        var slots = POSITIONS.Select((position, index) => new ApprovalSlot(index, position,
            index == GAP_SLOT ? null : club[index].Id, $"Owned {index}", 70, index != GAP_SLOT, null, 0)).ToList();
        var census = SquadCensus.Of(RequirementParser.Parse(TWO_CLUBS_AT_MOST).Single(), slots, club,
            [Pinned(GapOutcome.Pending)], ChemistryThresholds.Default, TeamLinks.None);

        Assert.NotNull(census);
        Assert.Equal(POSITIONS.Length, census.Squad.Count);
        Assert.Equal(POSITIONS[GAP_SLOT], census.Squad[GAP_SLOT].PreferredPosition);
        Assert.Equal(PINNED_CLUB, census.Squad[GAP_SLOT].TeamId);
    }

    [Fact]
    public void AnApprovalWhoseOwnedCardHasLeftTheClubHasNoCensus()
    {
        var slots = POSITIONS.Select((position, index) => new ApprovalSlot(index, position, 900 + index,
            $"Owned {index}", 70, true, null, 0)).ToList();

        Assert.Null(SquadCensus.Of(RequirementParser.Parse(TWO_CLUBS_AT_MOST).Single(), slots, Owned(), [],
            ChemistryThresholds.Default, TeamLinks.None));
    }

    private static TradeState Due(string tradeId, uint minimumBid, int secondsLeft = 10)
    {
        return new TradeState(tradeId, "active", "none", 0, secondsLeft, minimumBid, 77);
    }

    private static TradeState Ended(string tradeId, uint finalBid, string bidState)
    {
        return new TradeState(tradeId, "closed", bidState, finalBid, 0, 0, 77);
    }
}
