using Automation.Trading;

namespace Automation.Tests;

public class ClubInventoryTests
{
    private const string CLUB = """
        {"itemData":[
          {"id":942808952427,"timestamp":1789717959,"formation":"f3412","untradeable":true,"assetId":229237,"rating":83,"itemType":"player","resourceId":229237,"owners":1,"discardValue":0,"untradableDiscardValue":589,"itemState":"free","cardsubtypeid":1,"lastSalePrice":0,"injuryType":"none","injuryGames":0,"preferredPosition":"CB","contract":7,"teamid":131682,"rareflag":0,"playStyle":250,"leagueId":31,"loyaltyBonus":1,"pile":7,"nation":47,"marketDataMinPrice":600,"marketDataMaxPrice":10000,"resourceGameYear":2027,"attributeArray":[75,48,70,74,84,81],"statsArray":[0,0,0,0,0],"skillmoves":1,"weakfootabilitytypecode":4,"preferredfoot":1,"marketAverage":819,"possiblePositions":["CB"],"gender":0,"baseTraits":[10],"plusRoles":[11,13],"gradingScore":410,"isCollected":true}
        ]}
        """;

    private const string MIXED = """
        {"itemData":[
          {"id":1,"assetId":10,"resourceId":10,"rating":70,"itemType":"player","preferredPosition":"ST","possiblePositions":["ST","CF"],"untradeable":false,"itemState":"free","pile":7},
          {"id":2,"assetId":20,"itemType":"kit","rating":0},
          {"id":3,"assetId":30}
        ]}
        """;

    [Fact]
    public void ParsesAClubItemIntoARow()
    {
        var players = ClubInventory.Parse(CLUB);
        var player = players.Single();

        Assert.Equal(942808952427, player.Id);
        Assert.Equal(229237, player.AssetId);
        Assert.Equal(229237, player.ResourceId);
        Assert.Equal(83, player.Rating);
        Assert.Equal("CB", player.PreferredPosition);
        Assert.Equal(new[] { "CB" }, player.PossiblePositions);
        Assert.Equal(131682, player.TeamId);
        Assert.Equal(31, player.LeagueId);
        Assert.Equal(47, player.Nation);
        Assert.Equal(0, player.RareFlag);
        Assert.Equal(1, player.CardSubTypeId);
        Assert.True(player.Untradeable);
        Assert.Equal("free", player.ItemState);
        Assert.Equal(7, player.Pile);
        Assert.Equal(819, player.MarketAverage);
    }

    [Fact]
    public void AnEmptyClubHasNoRows()
    {
        Assert.Empty(ClubInventory.Parse("""{"itemData":[]}"""));
    }

    [Theory]
    [InlineData("nope")]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("""{"itemData":{"id":1}}""")]
    public void AMalformedPayloadHasNoRows(string json)
    {
        Assert.Empty(ClubInventory.Parse(json));
    }

    [Fact]
    public void AnItemMissingOptionalFieldsKeepsItsIdentifiers()
    {
        var player = ClubInventory.Parse(MIXED).Single(entry => entry.Id == 3);

        Assert.Equal(30, player.AssetId);
        Assert.Equal(0, player.ResourceId);
        Assert.Equal(0, player.Rating);
        Assert.Equal(string.Empty, player.PreferredPosition);
        Assert.Empty(player.PossiblePositions);
        Assert.False(player.Untradeable);
        Assert.Equal(string.Empty, player.ItemState);
    }

    [Fact]
    public void ItemsThatAreNotPlayersAreLeftOut()
    {
        Assert.Equal(new long[] { 1, 3 }, ClubInventory.Parse(MIXED).Select(player => player.Id));
    }

    [Fact]
    public void EveryPossiblePositionIsKept()
    {
        var player = ClubInventory.Parse(MIXED).Single(entry => entry.Id == 1);

        Assert.Equal(new[] { "ST", "CF" }, player.PossiblePositions);
    }
}
