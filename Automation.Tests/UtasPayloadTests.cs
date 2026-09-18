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
    [InlineData("OPTIONS", "https://utas.mob.v1.prd.futc-ext.gcp.ea.com/ut/game/fc27/watchlist", CaptureKind.Other)]
    [InlineData("GET", "https://pin-river.data.ea.com/pinEvents", CaptureKind.Other)]
    [InlineData("OPTIONS", "https://utas.mob.v1.prd.futc-ext.gcp.ea.com/ut/game/fc27/transfermarket?num=21&start=0&type=player&definitionId=5", CaptureKind.Other)]
    [InlineData("OPTIONS", "https://utas.mob.v1.prd.futc-ext.gcp.ea.com/ut/game/fc27/trade/608459623171/bid", CaptureKind.Other)]
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
    public void FailedBidResponseCarriesTheServersReason()
    {
        var result = UtasPayloads.BidResult(470, """{"code":"470","reason":"Bid too low","string":"Permission Denied"}""", "1");

        Assert.Equal(BidOutcome.Failed, result?.Outcome);
        Assert.Equal("470 Permission Denied Bid too low", result?.Reason);
        Assert.Equal("461 <html>oops", UtasPayloads.BidResult(461, "<html>oops", "1")?.Reason);
    }
}
