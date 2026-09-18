namespace Automation.Trading;

public sealed record SnipeFilter(string Name, string Quality, string? Nationality = null, string? League = null,
    string? Club = null, string? Position = null);

public static class SnipeFilters
{
    public const string LIGUE_1 = "Ligue 1 McDonald's (FRA 1)";

    public static readonly IReadOnlyList<SnipeFilter> RING = new[]
    {
        new SnipeFilter("OM silvers", "Silver", League: LIGUE_1, Club: "OM"),
        new SnipeFilter("PSG silvers", "Silver", League: LIGUE_1, Club: "PSG")
    };

    public static SnipeFilter Next(IReadOnlyList<SnipeFilter> ring, string? lastName)
    {
        var last = ring.ToList().FindIndex(filter => filter.Name == lastName);

        return ring[(last + 1) % ring.Count];
    }
}
