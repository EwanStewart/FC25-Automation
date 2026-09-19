using Automation.Trading;

namespace Automation.Tests;

public class UtasPayloadTests
{
    private const string SEARCH = """
        {"auctionInfo":[
          {"tradeId":608459623170,"tradeState":"active","bidState":"none","expires":45,"currentBid":0,"startingBid":550,"buyNowPrice":600,"watched":false,
           "itemData":{"id":943223967342,"resourceId":239023,"rating":66,"preferredPosition":"CM","lastSalePrice":300,"marketAverage":355}},
          {"tradeId":608459623171,"tradeState":"active","bidState":"outbid","expires":90,"currentBid":700,"startingBid":300,"buyNowPrice":10000,"watched":true,
           "itemData":{"id":1,"resourceId":2,"rating":72,"preferredPosition":"GK","lastSalePrice":0,"marketAverage":0}},
          {"tradeId":608459623172,"tradeState":"active","bidState":"none","expires":12,"currentBid":0,"startingBid":300,"buyNowPrice":0,
           "itemData":{"id":3,"resourceId":4,"rating":65,"preferredPosition":"CB"}}
        ],"bidTokens":{}}
        """;

    private const string BID = """
        {"credits":13000,"auctionInfo":[{"tradeId":608459623171,"tradeState":"active","bidState":"highest","expires":9,"currentBid":750,"startingBid":300,"buyNowPrice":10000}]}
        """;

    [Fact]
    public void ParsesEveryAuctionInAResponse()
    {
        var auctions = UtasPayloads.ParseAuctions(SEARCH);

        Assert.Equal(3, auctions.Count);
        Assert.Equal("608459623170", auctions[0].TradeId);
        Assert.Equal(45, auctions[0].Expires);
        Assert.Equal(355u, auctions[0].MarketAverage);
        Assert.Equal("outbid", auctions[1].BidState);
        Assert.Equal(700u, auctions[1].CurrentBid);
        Assert.Empty(UtasPayloads.ParseAuctions("nope"));
    }

    [Fact]
    public void AsksAreTheBuyNowPricesOfListingsThatHaveOne()
    {
        Assert.Equal(new uint[] { 600, 10000 }, UtasPayloads.Asks(SEARCH));
    }

    [Theory]
    [InlineData("GET", "https://utas.mob.v1.prd.futc-ext.gcp.ea.com/ut/game/fc27/transfermarket?num=21&start=0&type=player&definitionId=5", CaptureKind.Search)]
    [InlineData("GET", "https://utas.mob.v1.prd.futc-ext.gcp.ea.com/ut/game/fc27/trade/status/lite?tradeIds=1,2", CaptureKind.TradeStatus)]
    [InlineData("PUT", "https://utas.mob.v1.prd.futc-ext.gcp.ea.com/ut/game/fc27/trade/608459623171/bid", CaptureKind.Bid)]
    [InlineData("PUT", "https://utas.mob.v1.prd.futc-ext.gcp.ea.com/ut/game/fc27/auctionhouse", CaptureKind.Bid)]
    [InlineData("GET", "https://utas.mob.v1.prd.futc-ext.gcp.ea.com/ut/game/fc27/watchlist", CaptureKind.Watchlist)]
    [InlineData("PUT", "https://utas.mob.v1.prd.futc-ext.gcp.ea.com/ut/game/fc27/watchlist", CaptureKind.Watch)]
    [InlineData("DELETE", "https://utas.mob.v1.prd.futc-ext.gcp.ea.com/ut/game/fc27/watchlist?tradeId=1,2", CaptureKind.Unwatch)]
    [InlineData("OPTIONS", "https://utas.mob.v1.prd.futc-ext.gcp.ea.com/ut/game/fc27/watchlist", CaptureKind.Other)]
    [InlineData("GET", "https://pin-river.data.ea.com/pinEvents", CaptureKind.Other)]
    [InlineData("OPTIONS", "https://utas.mob.v1.prd.futc-ext.gcp.ea.com/ut/game/fc27/transfermarket?num=21&start=0&type=player&definitionId=5", CaptureKind.Other)]
    [InlineData("OPTIONS", "https://utas.mob.v1.prd.futc-ext.gcp.ea.com/ut/game/fc27/trade/608459623171/bid", CaptureKind.Other)]
    [InlineData("POST", "https://utas.mob.v1.prd.futc-ext.gcp.ea.com/ut/game/fc27/club", CaptureKind.Club)]
    [InlineData("OPTIONS", "https://utas.mob.v1.prd.futc-ext.gcp.ea.com/ut/game/fc27/club", CaptureKind.Other)]
    [InlineData("GET", "https://utas.mob.v1.prd.futc-ext.gcp.ea.com/ut/game/fc27/squad/active", CaptureKind.ActiveSquad)]
    [InlineData("OPTIONS", "https://utas.mob.v1.prd.futc-ext.gcp.ea.com/ut/game/fc27/squad/active", CaptureKind.Other)]
    [InlineData("GET", "https://utas.mob.v1.prd.futc-ext.gcp.ea.com/ut/game/fc27/transfermarket?num=21&start=0&type=player&club=112658", CaptureKind.Search)]
    public void ClassifiesTheAppsRequests(string method, string url, CaptureKind expected)
    {
        Assert.Equal(expected, UtasPayloads.Classify(method, url));
    }

    [Fact]
    public void BidResponseSettlesTheOutcomeForTheTradeItNames()
    {
        Assert.Equal(BidOutcome.Registered, UtasPayloads.BidResult(200, BID, "608459623171").Outcome);
        Assert.Equal(BidOutcome.Overtaken, UtasPayloads.BidResult(200, SEARCH, "608459623171").Outcome);
        Assert.Null(UtasPayloads.BidResult(200, BID, "999"));
    }

    [Fact]
    public void ABuyResponseSaysWhetherTheCardIsOurs()
    {
        const string bought = """
            {"credits":9000,"auctionInfo":[{"tradeId":608459623171,"tradeState":"closed","bidState":"buyNow","expires":0,"currentBid":600,"startingBid":300,"buyNowPrice":600}]}
            """;

        Assert.True(UtasPayloads.BuyResult(200, bought, "608459623171")?.Bought);
        Assert.False(UtasPayloads.BuyResult(200, SEARCH, "608459623170")?.Bought);
        Assert.False(UtasPayloads.BuyResult(461, bought, "608459623171")?.Bought);
        Assert.Null(UtasPayloads.BuyResult(200, bought, "999"));
    }

    [Fact]
    public void ItemAttributesComeOffTheListingSoACardCanBeCheckedAgainstASpecification()
    {
        const string body = """
            {"auctionInfo":[{"tradeId":1,"tradeState":"active","bidState":"none","expires":60,"currentBid":0,"startingBid":300,"buyNowPrice":0,
             "itemData":{"id":943223967342,"assetId":239023,"rating":66,"preferredPosition":"CM","possiblePositions":["CM","CDM"],"teamid":11,"leagueId":13,"nation":14,"rareflag":1}}]}
            """;
        var auction = UtasPayloads.ParseAuctions(body)[0];

        Assert.Equal(943223967342, auction.ItemId);
        Assert.Equal(239023, auction.AssetId);
        Assert.Equal(11, auction.TeamId);
        Assert.Equal(13, auction.LeagueId);
        Assert.Equal(14, auction.NationId);
        Assert.Equal(1, auction.RareFlag);
        Assert.Equal(["CM", "CDM"], auction.PossiblePositions);
    }

    [Fact]
    public void ListingsWithoutItemAttributesReadAsUnknownRatherThanFailing()
    {
        var auction = UtasPayloads.ParseAuctions(SEARCH)[2];

        Assert.Equal(0, auction.TeamId);
        Assert.Empty(auction.PossiblePositions ?? []);
    }

    [Fact]
    public void FailedBidResponseCarriesTheServersReason()
    {
        var result = UtasPayloads.BidResult(470, """{"code":"470","reason":"Bid too low","string":"Permission Denied"}""", "1");

        Assert.Equal(BidOutcome.Failed, result?.Outcome);
        Assert.Equal("470 Permission Denied Bid too low", result?.Reason);
        Assert.Equal("461 <html>oops", UtasPayloads.BidResult(461, "<html>oops", "1")?.Reason);
    }
}
