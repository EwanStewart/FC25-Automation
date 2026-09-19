using Automation.Sbc.Fulfilment;
using Automation.Trading;

namespace Automation.Tests;

public class FulfilmentReadingTests
{
    private const string SQUAD = """
        {"challengeId":1234,"squad":{"id":9,"formation":"f442","rating":75,"chemistry":21,"manager":{},
         "players":[
           {"index":0,"itemData":{"id":1001,"assetId":501,"rating":70,"preferredPosition":"GK","itemState":"free"}},
           {"index":1,"itemData":{"id":0,"assetId":0,"rating":0,"preferredPosition":"","itemState":"invalid"}},
           {"index":2,"itemData":{"id":1002,"assetId":502,"rating":72,"preferredPosition":"CB","itemState":"free"}}
         ]}}
        """;

    [Fact]
    public void TheChallengeSquadReadsBackAsSlotsWithTheirCards()
    {
        var view = SquadReader.Read(SQUAD);

        Assert.Equal(1234, view?.ChallengeId);
        Assert.Equal("f442", view?.Formation);
        Assert.Equal(3, view?.Slots.Count);
        Assert.Equal(1001, view?.Slots[0].ItemId);
        Assert.True(view?.Slots[0].Filled);
    }

    [Fact]
    public void AnEmptySlotReadsBackAsUnfilled()
    {
        var view = SquadReader.Read(SQUAD);

        Assert.False(view?.Slots[1].Filled);
        Assert.Equal(0, view?.Slots[1].ItemId);
    }

    [Fact]
    public void ASquadBodyThatIsNotOneReadsAsNothingRatherThanThrowing()
    {
        Assert.Null(SquadReader.Read("<html>oops"));
        Assert.Null(SquadReader.Read("{}"));
    }

    [Fact]
    public void TheChallengeSquadResponseIsRecognisedInTheTraffic()
    {
        Assert.Equal(CaptureKind.SbcSquad,
            UtasPayloads.Classify("POST", "https://utas.mob.v1.fut.ea.com/ut/game/fc27/sbs/challenge/1234"));
        Assert.NotEqual(CaptureKind.SbcSquad,
            UtasPayloads.Classify("GET", "https://utas.mob.v1.fut.ea.com/ut/game/fc27/sbs/sets"));
    }

    [Fact]
    public void AnInProgressChallengeReadsItsSquadBackOverAGetAndIsRecognisedToo()
    {
        Assert.Equal(CaptureKind.SbcSquad,
            UtasPayloads.Classify("GET", "https://utas.mob.v1.fut.ea.com/ut/game/fc27/sbs/challenge/14/squad"));
        Assert.NotEqual(CaptureKind.SbcSquad,
            UtasPayloads.Classify("PUT", "https://utas.mob.v1.fut.ea.com/ut/game/fc27/sbs/challenge/14"));
    }

    [Fact]
    public void TheChallengeIdIsReadFromTheSquadUrlWhenTheBodyCarriesNone()
    {
        Assert.Equal(14, SquadReader.ChallengeIn("https://utas.mob.v1.fut.ea.com/ut/game/fc27/sbs/challenge/14"));
        Assert.Equal(14,
            SquadReader.ChallengeIn("https://utas.mob.v1.fut.ea.com/ut/game/fc27/sbs/challenge/14/squad"));
        Assert.Equal(0, SquadReader.ChallengeIn("https://utas.mob.v1.fut.ea.com/ut/game/fc27/sbs/sets"));
    }

    [Fact]
    public void ATargetRowReportsWhatTheModelSaysAboutTheTrade()
    {
        var state = TargetRowState.Of("listFUTItem has-auction-data",
            new ModelSnapshot("t1", 30, "highest", "active", 700, 300, "Name"), 700);

        Assert.Equal("t1", state?.TradeId);
        Assert.Equal("active", state?.State);
        Assert.Equal("highest", state?.BidState);
        Assert.Equal(700u, state?.CurrentBid);
    }

    [Fact]
    public void AWonRowReadsAsAClosedTradeWeLed()
    {
        var state = TargetRowState.Of("listFUTItem has-auction-data won",
            new ModelSnapshot("t1", 0, "highest", "", 700, 300, "Name"), 700);

        Assert.Equal("closed", state?.State);
        Assert.Equal("highest", state?.BidState);
    }

    [Fact]
    public void AnExpiredRowReadsAsAnEndedTrade()
    {
        var state = TargetRowState.Of("listFUTItem has-auction-data expired",
            new ModelSnapshot("t1", 0, "outbid", "", 900, 300, "Name"), 900);

        Assert.Equal("expired", state?.State);
    }

    [Fact]
    public void ARowWithNoTrustedModelIsNotReportedAtAll()
    {
        Assert.Null(TargetRowState.Of("listFUTItem has-auction-data", null, 0));
    }
}
