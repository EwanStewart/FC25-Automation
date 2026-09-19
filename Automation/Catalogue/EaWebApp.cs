using System.Text.RegularExpressions;

namespace Automation.Catalogue;

public sealed record EaContentVersion(string Guid, string Year);

public static class EaWebApp
{
    public const string WEB_APP_URL = "https://www.ea.com/ea-sports-fc/ultimate-team/web-app/";
    private const string CONTENT_PATH = "content";
    private const string PLAYERS_PATH = "fut/items/web/players.json";
    private const string GUID_PATTERN = @"fut_guid\s*=\s*[""']([^""']+)[""']";
    private const string YEAR_PATTERN = @"fut_year\s*=\s*[""']?(\d{4})[""']?";

    public static EaContentVersion ReadContentVersion(string html)
    {
        return new EaContentVersion(Capture(html, GUID_PATTERN, "fut_guid"), Capture(html, YEAR_PATTERN, "fut_year"));
    }

    public static string PlayersUrl(EaContentVersion version)
    {
        return $"{WEB_APP_URL}{CONTENT_PATH}/{version.Guid}/{version.Year}/{PLAYERS_PATH}";
    }

    private static string Capture(string html, string pattern, string name)
    {
        var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);

        if (!match.Success)
            throw new CatalogueFormatException($"The web app page carries no {name}, so the content version is unknown.");

        return match.Groups[1].Value;
    }
}
