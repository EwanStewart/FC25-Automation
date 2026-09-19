using System.Net;
using Automation.Catalogue;

namespace Automation.Tests;

public class EaWebAppTests
{
    private const string WEB_APP_HTML = """
                                        <script type="text/javascript">
                                        	window.fut_resourceRoot = "https://www.ea.com";
                                        	window.fut_resourceBase = "/ea-sports-fc/ultimate-team/web-app/content/";
                                        	window.fut_guid = "27A3C9F1-6B2E-4D7A-8C1F-2E9B5A4D6C7E";
                                        	window.fut_year = "2027";
                                        </script>
                                        """;

    [Fact]
    public void TheContentVersionComesOutOfTheWebAppHtml()
    {
        var version = EaWebApp.ReadContentVersion(WEB_APP_HTML);

        Assert.Equal("27A3C9F1-6B2E-4D7A-8C1F-2E9B5A4D6C7E", version.Guid);
        Assert.Equal("2027", version.Year);
    }

    [Fact]
    public void HtmlWithoutAGuidIsRejected()
    {
        var error = Assert.Throws<CatalogueFormatException>(() =>
            EaWebApp.ReadContentVersion("<html><body>no content version here</body></html>"));

        Assert.Contains("fut_guid", error.Message);
    }

    [Fact]
    public void HtmlWithoutAYearIsRejected()
    {
        var error = Assert.Throws<CatalogueFormatException>(() =>
            EaWebApp.ReadContentVersion("window.fut_guid = \"ABC\";"));

        Assert.Contains("fut_year", error.Message);
    }

    [Fact]
    public void ThePlayersUrlIsBuiltFromTheLiveContentVersion()
    {
        var url = EaWebApp.PlayersUrl(new EaContentVersion("27A3C9F1-6B2E-4D7A-8C1F-2E9B5A4D6C7E", "2027"));

        Assert.Equal(
            "https://www.ea.com/ea-sports-fc/ultimate-team/web-app/content/27A3C9F1-6B2E-4D7A-8C1F-2E9B5A4D6C7E/2027/fut/items/web/players.json",
            url);
    }
}

public class EaPlayerCatalogueTests
{
    [Fact]
    public void BothArraysAreRead()
    {
        var players = EaPlayerCatalogue.ParsePlayers(CatalogueFixture.Read("EaPlayersSample.json"));

        Assert.Equal(6, players.Count);
        Assert.Contains(players, player => player.SourceId == 41);
        Assert.Contains(players, player => player.SourceId == 229237);
    }

    [Fact]
    public void ARecordBecomesAnAssetKeyedPlayer()
    {
        var players = EaPlayerCatalogue.ParsePlayers(CatalogueFixture.Read("EaPlayersSample.json"));
        var akanji = players.Single(player => player.SourceId == 229237);

        Assert.Equal(229237, akanji.AssetId);
        Assert.Equal("Manuel Akanji", akanji.Name);
        Assert.Null(akanji.CommonName);
        Assert.Equal(83, akanji.Rating);
    }

    [Fact]
    public void ACommonNameIsKeptWhenTheRecordCarriesOne()
    {
        var players = EaPlayerCatalogue.ParsePlayers(CatalogueFixture.Read("EaPlayersSample.json"));

        Assert.Equal("Iniesta", players.Single(player => player.SourceId == 41).CommonName);
        Assert.Equal("Andrés Iniesta Luján", players.Single(player => player.SourceId == 41).Name);
    }

    [Fact]
    public void AccentedNamesSurviveTheRead()
    {
        var players = EaPlayerCatalogue.ParsePlayers(CatalogueFixture.Read("EaPlayersSample.json"));

        Assert.Equal("Kylian Mbappé", players.Single(player => player.SourceId == 231747).Name);
    }

    [Fact]
    public void TheFieldsThisSourceDoesNotCarryStayEmpty()
    {
        var players = EaPlayerCatalogue.ParsePlayers(CatalogueFixture.Read("EaPlayersSample.json"));
        var player = players.Single(source => source.SourceId == 229237);

        Assert.Null(player.ClubId);
        Assert.Null(player.LeagueId);
        Assert.Null(player.NationId);
        Assert.Null(player.RarityId);
        Assert.Null(player.PreferredPosition);
        Assert.Null(player.ResourceId);
        Assert.Empty(player.AlternatePositions);
    }

    [Fact]
    public void ARecordWithoutAnIdIsDropped()
    {
        const string json = """{"Players": [{"f": "No", "l": "Id", "r": 70}, {"id": 5, "f": "Has", "l": "Id", "r": 71}]}""";

        var players = EaPlayerCatalogue.ParsePlayers(json);

        Assert.Single(players);
        Assert.Equal(5, players[0].SourceId);
    }

    [Fact]
    public void APayloadWithNeitherArrayIsRejected()
    {
        Assert.Throws<CatalogueFormatException>(() => EaPlayerCatalogue.ParsePlayers("""{"Teams": []}"""));
    }

    [Fact]
    public void BrokenJsonIsRejected()
    {
        Assert.Throws<CatalogueFormatException>(() => EaPlayerCatalogue.ParsePlayers("not json"));
    }
}

public class EaPlayerSourceTests
{
    [Fact]
    public async Task TheWholeFileArrivesAsOnePageCarryingItsTag()
    {
        StubTransport transport = new();
        using HttpClient client = new(transport);
        EaPlayerSource source = new(client);

        var page = await source.FetchPageAsync(1, null, CancellationToken.None);

        Assert.Equal(EaPlayerSource.SOURCE_NAME, source.Name);
        Assert.Equal(1, page.PageCurrent);
        Assert.Equal(1, page.PageTotal);
        Assert.Equal(6, page.CountTotal);
        Assert.Equal(6, page.Players.Count);
        Assert.Equal("Mon, 14 Sep 2026 23:18:58 GMT", page.Tag);
        Assert.False(page.Unchanged);
    }

    [Fact]
    public async Task TheContentVersionIsReadFromTheWebAppRatherThanHardcoded()
    {
        StubTransport transport = new();
        using HttpClient client = new(transport);
        EaPlayerSource source = new(client);

        await source.FetchPageAsync(1, null, CancellationToken.None);

        Assert.Equal("https://www.ea.com/ea-sports-fc/ultimate-team/web-app/", transport.SeenUrls[0]);
        Assert.Equal(
            "https://www.ea.com/ea-sports-fc/ultimate-team/web-app/content/27A3C9F1-6B2E-4D7A-8C1F-2E9B5A4D6C7E/2027/fut/items/web/players.json",
            transport.SeenUrls[1]);
    }

    [Fact]
    public async Task AKnownTagIsSentAsIfModifiedSince()
    {
        StubTransport transport = new();
        using HttpClient client = new(transport);
        EaPlayerSource source = new(client);

        await source.FetchPageAsync(1, "Mon, 14 Sep 2026 23:18:58 GMT", CancellationToken.None);

        Assert.Equal("Mon, 14 Sep 2026 23:18:58 GMT", transport.SeenIfModifiedSince);
    }

    [Fact]
    public async Task NotModifiedComesBackAsAnUnchangedPage()
    {
        StubTransport transport = new() { PlayersStatus = HttpStatusCode.NotModified };
        using HttpClient client = new(transport);
        EaPlayerSource source = new(client);

        var page = await source.FetchPageAsync(1, "Mon, 14 Sep 2026 23:18:58 GMT", CancellationToken.None);

        Assert.True(page.Unchanged);
        Assert.Empty(page.Players);
        Assert.Equal("Mon, 14 Sep 2026 23:18:58 GMT", page.Tag);
    }

    [Fact]
    public async Task AFailedFetchIsReported()
    {
        StubTransport transport = new() { PlayersStatus = HttpStatusCode.NotFound };
        using HttpClient client = new(transport);
        EaPlayerSource source = new(client);

        await Assert.ThrowsAsync<CatalogueRequestException>(() =>
            source.FetchPageAsync(1, null, CancellationToken.None));
    }

    private sealed class StubTransport : HttpMessageHandler
    {
        private const string HTML = """
                                    window.fut_guid = "27A3C9F1-6B2E-4D7A-8C1F-2E9B5A4D6C7E";
                                    window.fut_year = "2027";
                                    """;

        public List<string> SeenUrls { get; } = [];
        public string? SeenIfModifiedSince { get; private set; }
        public HttpStatusCode PlayersStatus { get; init; } = HttpStatusCode.OK;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var url = request.RequestUri?.ToString() ?? string.Empty;
            SeenUrls.Add(url);

            if (request.Headers.IfModifiedSince.HasValue)
                SeenIfModifiedSince = request.Headers.IfModifiedSince.Value.ToString("R");

            return Task.FromResult(url.EndsWith("players.json", StringComparison.Ordinal)
                ? PlayersResponse()
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(HTML) });
        }

        private HttpResponseMessage PlayersResponse()
        {
            HttpResponseMessage result = new(PlayersStatus)
            {
                Content = new StringContent(PlayersStatus == HttpStatusCode.OK
                    ? CatalogueFixture.Read("EaPlayersSample.json")
                    : string.Empty)
            };
            result.Content.Headers.LastModified = new DateTimeOffset(2026, 9, 14, 23, 18, 58, TimeSpan.Zero);

            return result;
        }
    }
}
