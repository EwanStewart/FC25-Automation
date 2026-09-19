namespace Automation.Catalogue;

public sealed class FutDbPlayerSource : IPlayerSource
{
    public const string SOURCE_NAME = "futdb";
    private readonly HttpClient client_;
    private readonly string apiKey_;

    public FutDbPlayerSource(HttpClient client, string apiKey)
    {
        client_ = client;
        apiKey_ = CatalogueSecrets.RequireApiKey(apiKey);
    }

    public string Name => SOURCE_NAME;

    public async Task<CataloguePage> FetchPageAsync(int page, CancellationToken cancellationToken)
    {
        var body = await ReadPageAsync(page, cancellationToken);

        return CatalogueParser.ParsePage(body);
    }

    private async Task<string> ReadPageAsync(int page, CancellationToken cancellationToken)
    {
        using var request = FutDbRequest.BuildPlayersRequest(page, apiKey_);
        using var response = await client_.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        FutDbRequest.EnsureAccepted((int)response.StatusCode, body);

        return body;
    }
}
