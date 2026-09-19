using System.Globalization;
using System.Net;

namespace Automation.Catalogue;

public sealed class EaPlayerSource : IPlayerSource
{
    public const string SOURCE_NAME = "ea-web-app";
    private const int ONLY_PAGE = 1;
    private readonly HttpClient client_;

    public EaPlayerSource(HttpClient client)
    {
        client_ = client;
    }

    public string Name => SOURCE_NAME;

    public async Task<CataloguePage> FetchPageAsync(int page, string? tag, CancellationToken cancellationToken)
    {
        var version = await ReadContentVersionAsync(cancellationToken);

        return await ReadPlayersAsync(EaWebApp.PlayersUrl(version), tag, cancellationToken);
    }

    private async Task<EaContentVersion> ReadContentVersionAsync(CancellationToken cancellationToken)
    {
        using var response = await client_.GetAsync(EaWebApp.WEB_APP_URL, cancellationToken);
        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureAccepted((int)response.StatusCode, EaWebApp.WEB_APP_URL);

        return EaWebApp.ReadContentVersion(html);
    }

    private async Task<CataloguePage> ReadPlayersAsync(string url, string? tag, CancellationToken cancellationToken)
    {
        using var request = BuildRequest(url, tag);
        using var response = await client_.SendAsync(request, cancellationToken);
        var result = response.StatusCode == HttpStatusCode.NotModified
            ? CataloguePlan.UnchangedPage(tag)
            : await ReadPageAsync(response, url, cancellationToken);

        return result;
    }

    private static async Task<CataloguePage> ReadPageAsync(HttpResponseMessage response, string url,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureAccepted((int)response.StatusCode, url);
        var players = EaPlayerCatalogue.ParsePlayers(body);

        return new CataloguePage(ONLY_PAGE, ONLY_PAGE, players.Count, players, LastModified(response));
    }

    private static HttpRequestMessage BuildRequest(string url, string? tag)
    {
        HttpRequestMessage result = new(HttpMethod.Get, url);

        if (DateTimeOffset.TryParse(tag, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var since))
            result.Headers.IfModifiedSince = since;

        return result;
    }

    private static string? LastModified(HttpResponseMessage response)
    {
        var stamp = response.Content.Headers.LastModified;

        return stamp?.ToString("R", CultureInfo.InvariantCulture);
    }

    private static void EnsureAccepted(int statusCode, string url)
    {
        if (statusCode != (int)HttpStatusCode.OK)
            throw new CatalogueRequestException($"The EA web app answered {statusCode} for {url}.");
    }
}
