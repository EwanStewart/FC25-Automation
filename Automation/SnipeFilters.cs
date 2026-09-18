namespace Automation.Trading;

public sealed record SnipeFilter(string Name, string Quality, string? Nationality = null, string? League = null,
    string? Club = null, string? Position = null);

public static class SnipeFilters
{
    public static readonly IReadOnlyList<SnipeFilter> RING = new[]
    {
        new SnipeFilter("Scotland bronzes", "Bronze", Nationality: "Scotland"),
        new SnipeFilter("Scotland silvers", "Silver", Nationality: "Scotland")
    };

    public static SnipeFilter Next(IReadOnlyList<SnipeFilter> ring, string? lastName)
    {
        var last = ring.ToList().FindIndex(filter => filter.Name == lastName);

        return ring[(last + 1) % ring.Count];
    }
}
