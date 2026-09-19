using Automation.Trading;

namespace Automation.Tests;

public class SnipeFiltersTests
{
    private static readonly SnipeFilter[] Ring =
    {
        new("first", "Silver", Nationality: "England"),
        new("second", "Silver", League: "Liga Portugal (POR 1)"),
        new("third", "Bronze", Position: "GK")
    };

    [Fact]
    public void NextWalksTheRingFromTheLastFilterName()
    {
        Assert.Equal("first", SnipeFilters.Next(Ring, null)?.Name);
        Assert.Equal("second", SnipeFilters.Next(Ring, "first")?.Name);
        Assert.Equal("first", SnipeFilters.Next(Ring, "third")?.Name);
        Assert.Equal("first", SnipeFilters.Next(Ring, "England")?.Name);
    }

    [Fact]
    public void AnEmptyRingMeansNoSnipePass()
    {
        Assert.Null(SnipeFilters.Next(Array.Empty<SnipeFilter>(), null));
        Assert.Null(SnipeFilters.Next(Array.Empty<SnipeFilter>(), "Scotland cards"));
    }

    [Fact]
    public void RingSnipesAnyBundesligaManager()
    {
        var managers = Assert.Single(SnipeFilters.RING);

        Assert.Equal("Bundesliga managers", managers.Name);
        Assert.Equal(SnipeMarket.Managers, managers.Market);
        Assert.Equal("Bundesliga (GER 1)", managers.League);
        Assert.Null(managers.Quality);
        Assert.Null(managers.Nationality);
        Assert.Null(managers.Club);
        Assert.Null(managers.Position);
    }

    [Fact]
    public void ManagerCeilingClearsThreeHundredCoinsAndStopsAtAThousand()
    {
        var managers = Assert.Single(SnipeFilters.RING);

        Assert.Equal(300u, managers.MarginCoins);
        Assert.Equal(1000u, managers.MaxBid);
        Assert.Equal(ResaleBasis.LowestAsk, managers.Resale);
        Assert.Equal(900u, Snipe.Ceiling(1300, managers.MarginCoins, managers.MaxBid));
        Assert.Equal(1000u, Snipe.Ceiling(1400, managers.MarginCoins, managers.MaxBid));
        Assert.Equal(1000u, Snipe.Ceiling(9000, managers.MarginCoins, managers.MaxBid));
        Assert.Equal(0u, Snipe.Ceiling(300, managers.MarginCoins, managers.MaxBid));
    }

    [Fact]
    public void AFilterSearchesPlayersOnTheStandardLimitsUnlessItSaysOtherwise()
    {
        SnipeFilter plain = new("plain");

        Assert.Equal(SnipeMarket.Players, plain.Market);
        Assert.Equal(BiddingStrategy.MARGIN_COINS, plain.MarginCoins);
        Assert.Equal(BiddingStrategy.SNIPE_MAX_BID, plain.MaxBid);
        Assert.Equal(ResaleBasis.SecondLowestAsk, plain.Resale);
    }

    [Fact]
    public void RingFiltersAreNamedDistinctlyAndChooseALeagueBeforeAClub()
    {
        Assert.Equal(SnipeFilters.RING.Count, SnipeFilters.RING.Select(filter => filter.Name).Distinct().Count());
        Assert.All(SnipeFilters.RING.Where(filter => filter.Club != null), filter => Assert.NotNull(filter.League));
    }
}
