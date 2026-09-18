namespace Automation.Trading;

public sealed record SnipeFilter(string Name, string? Quality = null, string? Nationality = null,
    string? League = null, string? Club = null, string? Position = null);

public static class SnipeFilters
{
    public static readonly IReadOnlyList<SnipeFilter> RING = Array.Empty<SnipeFilter>();

    public static SnipeFilter? Next(IReadOnlyList<SnipeFilter> ring, string? lastName)
    {
        var last = ring.ToList().FindIndex(filter => filter.Name == lastName);

        return ring.Count == 0 ? null : ring[(last + 1) % ring.Count];
    }
}
