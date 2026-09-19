namespace Automation.Catalogue;

public static class FutDbRequest
{
    public const string API_KEY_HEADER = "X-AUTH-TOKEN";
    public const string RETRY_AFTER_HEADER = "x-ratelimit-retry-after";
    public const string BASE_URL = "https://api.fut-db.com";
    private const string PLAYERS_PATH = "/api/players";
    private const int FIRST_PAGE = 1;
    private const int ACCEPTED = 200;
    private const int UNAUTHORISED = 401;
    private const int THROTTLED = 429;
    private const int BODY_EXCERPT_LENGTH = 200;

    public static string PlayersUrl(int page)
    {
        if (page < FIRST_PAGE) throw new ArgumentOutOfRangeException(nameof(page), page, "Paging starts at page one.");

        return $"{BASE_URL}{PLAYERS_PATH}?page={page}";
    }

    public static HttpRequestMessage BuildPlayersRequest(int page, string apiKey)
    {
        HttpRequestMessage result = new(HttpMethod.Get, PlayersUrl(page));
        result.Headers.Add(API_KEY_HEADER, CatalogueSecrets.RequireApiKey(apiKey));

        return result;
    }

    public static void EnsureAccepted(int statusCode, string body, string? retryAfter)
    {
        if (statusCode == UNAUTHORISED)
            throw new CatalogueConfigurationException(
                $"FUT-DB rejected {CatalogueSecrets.FUT_DB_KEY}. Check the key in the .env file at the root of the repository.");

        if (statusCode != ACCEPTED)
            throw new CatalogueRequestException($"FUT-DB answered {statusCode}: {Excerpt(body)}{Quota(statusCode, retryAfter)}");
    }

    public static string? RetryAfter(HttpResponseMessage response)
    {
        var present = response.Headers.TryGetValues(RETRY_AFTER_HEADER, out var values);

        return present ? values!.FirstOrDefault() : null;
    }

    private static string Quota(int statusCode, string? retryAfter)
    {
        var throttled = statusCode == THROTTLED && !string.IsNullOrWhiteSpace(retryAfter);

        return throttled ? $" The quota returns at {retryAfter}." : string.Empty;
    }

    private static string Excerpt(string body)
    {
        var trimmed = body.Trim();

        return trimmed.Length <= BODY_EXCERPT_LENGTH ? trimmed : trimmed[..BODY_EXCERPT_LENGTH];
    }
}
