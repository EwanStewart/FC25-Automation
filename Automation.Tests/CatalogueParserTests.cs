using Automation.Catalogue;

namespace Automation.Tests;

public class CatalogueParserTests
{
    [Fact]
    public void APageCarriesItsPlaceInThePaging()
    {
        var page = CatalogueParser.ParsePage(CatalogueFixture.Read("FutDbPlayersPage1.json"));

        Assert.Equal(1, page.PageCurrent);
        Assert.Equal(2, page.PageTotal);
        Assert.Equal(5, page.CountTotal);
        Assert.Equal(3, page.Players.Count);
    }

    [Fact]
    public void AFullyPopulatedPlayerKeepsEveryCataloguedField()
    {
        var page = CatalogueParser.ParsePage(CatalogueFixture.Read("FutDbPlayersPage1.json"));
        var player = page.Players[0];

        Assert.Equal(226, player.DefinitionId);
        Assert.Equal(50553196L, player.ResourceId);
        Assert.Equal("Kevin De Bruyne", player.Name);
        Assert.Equal("De Bruyne", player.CommonName);
        Assert.Equal(91, player.Rating);
        Assert.Equal("CM", player.PreferredPosition);
        Assert.Equal(["CAM", "CDM"], player.AlternatePositions);
        Assert.Equal(11, player.ClubId);
        Assert.Equal(13, player.LeagueId);
        Assert.Equal(7, player.NationId);
        Assert.Equal(1, player.RarityId);
        Assert.Equal("gold", player.CardColour);
    }

    [Fact]
    public void NullAndAbsentFieldsBecomeNullsRatherThanFailures()
    {
        var page = CatalogueParser.ParsePage(CatalogueFixture.Read("FutDbPlayersPage1.json"));
        var sparse = page.Players[2];

        Assert.Equal(2377, sparse.DefinitionId);
        Assert.Null(sparse.ResourceId);
        Assert.Null(sparse.CommonName);
        Assert.Null(sparse.Rating);
        Assert.Null(sparse.PreferredPosition);
        Assert.Null(sparse.ClubId);
        Assert.Null(sparse.CardColour);
        Assert.Empty(sparse.AlternatePositions);
    }

    [Fact]
    public void ARowWithoutADefinitionIdIsDropped()
    {
        var page = CatalogueParser.ParsePage(CatalogueFixture.Read("FutDbPlayersPage2.json"));

        Assert.Single(page.Players);
        Assert.Equal(3108, page.Players[0].DefinitionId);
    }

    [Fact]
    public void AnEnvelopeWithoutPagingIsRejected()
    {
        Assert.Throws<CatalogueFormatException>(() => CatalogueParser.ParsePage("{\"items\": []}"));
    }

    [Fact]
    public void AnEnvelopeWithoutItemsIsRejected()
    {
        const string json = "{\"pagination\": {\"pageCurrent\": 1, \"pageTotal\": 1, \"countTotal\": 0}}";

        Assert.Throws<CatalogueFormatException>(() => CatalogueParser.ParsePage(json));
    }

    [Fact]
    public void BrokenJsonIsRejected()
    {
        Assert.Throws<CatalogueFormatException>(() => CatalogueParser.ParsePage("not json"));
    }

    [Fact]
    public void AlternatePositionsAreStoredAsOneCommaSeparatedColumn()
    {
        var page = CatalogueParser.ParsePage(CatalogueFixture.Read("FutDbPlayersPage1.json"));

        Assert.Equal("CAM,CDM", CatalogueRow.AlternatePositions(page.Players[0]));
        Assert.Null(CatalogueRow.AlternatePositions(page.Players[1]));
    }
}
