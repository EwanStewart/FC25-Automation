namespace Automation.Trading;

public sealed record SnipeFilter(string Name, string? Quality = null, string? Nationality = null,
    string? League = null, string? Club = null, string? Position = null);

public static class SnipeFilters
{
    public static readonly IReadOnlyList<SnipeFilter> RING = new[]
    {
        new SnipeFilter("Scotland cards", Nationality: "Scotland")
    };

    public static SnipeFilter Next(IReadOnlyList<SnipeFilter> ring, string? lastName)
    {
        var last = ring.ToList().FindIndex(filter => filter.Name == lastName);

        return ring[(last + 1) % ring.Count];
    }
}
