namespace Automation.Trading;

public enum SnipeMarket
{
    Players,
    Managers
}

public sealed record SnipeFilter(string Name, string? Quality = null, string? Nationality = null,
    string? League = null, string? Club = null, string? Position = null,
    SnipeMarket Market = SnipeMarket.Players, uint MarginCoins = BiddingStrategy.MARGIN_COINS,
    uint MaxBid = BiddingStrategy.SNIPE_MAX_BID, ResaleBasis Resale = ResaleBasis.SecondLowestAsk,
    uint? PriceAgeMinutes = null);

public static class SnipeFilters
{
    public static readonly IReadOnlyList<SnipeFilter> RING = new[]
    {
        new SnipeFilter("Bundesliga managers", League: "Bundesliga (GER 1)", Market: SnipeMarket.Managers,
            MarginCoins: 300, MaxBid: 1000, Resale: ResaleBasis.LowestAsk, PriceAgeMinutes: 10)
    };

    public static SnipeFilter? Next(IReadOnlyList<SnipeFilter> ring, string? lastName)
    {
        var last = ring.ToList().FindIndex(filter => filter.Name == lastName);

        return ring.Count == 0 ? null : ring[(last + 1) % ring.Count];
    }
}
