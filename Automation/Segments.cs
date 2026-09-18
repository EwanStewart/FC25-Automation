namespace Automation.Trading;

public static class Segments
{
    public const string BADGES = "badges";
    public const string KITS = "kits";
    public const string ROLE_CLUB = "club";
    public const string ROLE_BEST = "best";
    public const string ROLE_EXPLORE = "explore";
    private const string PLAYERS_PREFIX = "players:";

    public static string Players(string nation)
    {
        return $"{PLAYERS_PREFIX}{nation}";
    }

    public static string? Nation(string segment)
    {
        string? result = null;

        if (segment.StartsWith(PLAYERS_PREFIX, StringComparison.Ordinal)) result = segment[PLAYERS_PREFIX.Length..];

        return result;
    }
}
