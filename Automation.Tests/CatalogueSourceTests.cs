using System.Net;
using Automation.Catalogue;

namespace Automation.Tests;

public class CatalogueSourceTests
{
    [Fact]
    public void AMissingKeyFailsByName()
    {
        var error = Assert.Throws<CatalogueConfigurationException>(() => CatalogueSecrets.RequireApiKey(null));

        Assert.Contains("FUT_DB_KEY", error.Message);
        Assert.Contains(".env", error.Message);
    }

    [Fact]
    public void ABlankKeyCountsAsMissing()
    {
        Assert.Throws<CatalogueConfigurationException>(() => CatalogueSecrets.RequireApiKey("   "));
        Assert.Throws<CatalogueConfigurationException>(() => CatalogueSecrets.RequireApiKey(string.Empty));
    }

    [Fact]
    public void APresentKeyIsHandedBack()
    {
        Assert.Equal("abc123", CatalogueSecrets.RequireApiKey("abc123"));
    }

    [Fact]
    public void APlayersRequestCarriesThePageAndTheKeyHeader()
    {
        using var request = FutDbRequest.BuildPlayersRequest(4, "abc123");

        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://api.fut-db.com/api/players?page=4", request.RequestUri?.ToString());
        Assert.Equal("abc123", request.Headers.GetValues(FutDbRequest.API_KEY_HEADER).Single());
    }

    [Fact]
    public void APageBelowTheFirstIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FutDbRequest.BuildPlayersRequest(0, "abc123"));
    }

    [Fact]
    public void ARejectedKeyReadsAsAConfigurationFailure()
    {
        var error = Assert.Throws<CatalogueConfigurationException>(() =>
            FutDbRequest.EnsureAccepted((int)HttpStatusCode.Unauthorized, "{\"message\": \"invalid token\"}", null));

        Assert.Contains("FUT_DB_KEY", error.Message);
    }

    [Fact]
    public void AThrottledPageReadsAsARequestFailure()
    {
        var error = Assert.Throws<CatalogueRequestException>(() =>
            FutDbRequest.EnsureAccepted((int)HttpStatusCode.TooManyRequests, "slow down", null));

        Assert.Contains("429", error.Message);
        Assert.Contains("slow down", error.Message);
    }

    [Fact]
    public void AThrottledPageNamesTheMomentTheQuotaReturns()
    {
        var error = Assert.Throws<CatalogueRequestException>(() => FutDbRequest.EnsureAccepted(
            (int)HttpStatusCode.TooManyRequests, "{\"code\": 429}", "2026-09-20T06:00:54+00:00"));

        Assert.Contains("2026-09-20T06:00:54+00:00", error.Message);
    }

    [Fact]
    public void AnAcceptedPagePassesThrough()
    {
        FutDbRequest.EnsureAccepted((int)HttpStatusCode.OK, "{}", null);
    }

    [Fact]
    public async Task TheSourceParsesWhateverTheTransportHandsBack()
    {
        var transport = new StubTransport(HttpStatusCode.OK, CatalogueFixture.Read("FutDbPlayersPage1.json"));
        using HttpClient client = new(transport);
        FutDbPlayerSource source = new(client, "abc123");

        var page = await source.FetchPageAsync(1, null, CancellationToken.None);

        Assert.Equal(FutDbPlayerSource.SOURCE_NAME, source.Name);
        Assert.Equal(3, page.Players.Count);
        Assert.Equal("abc123", transport.SeenKey);
        Assert.Equal("https://api.fut-db.com/api/players?page=1", transport.SeenUrl);
    }

    [Fact]
    public async Task TheSourceReportsAFailedStatusRatherThanParsingIt()
    {
        var transport = new StubTransport(HttpStatusCode.TooManyRequests, "{\"code\": 429}");
        using HttpClient client = new(transport);
        FutDbPlayerSource source = new(client, "abc123");

        var error = await Assert.ThrowsAsync<CatalogueRequestException>(() =>
            source.FetchPageAsync(1, null, CancellationToken.None));

        Assert.Contains("2026-09-20T06:00:54+00:00", error.Message);
    }

    private sealed class StubTransport(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public string? SeenKey { get; private set; }
        public string? SeenUrl { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            SeenUrl = request.RequestUri?.ToString();
            SeenKey = request.Headers.TryGetValues(FutDbRequest.API_KEY_HEADER, out var values)
                ? values.Single()
                : null;
            HttpResponseMessage response = new(status) { Content = new StringContent(body) };
            response.Headers.Add(FutDbRequest.RETRY_AFTER_HEADER, "2026-09-20T06:00:54+00:00");

            return Task.FromResult(response);
        }
    }
}
