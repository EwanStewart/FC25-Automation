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
        Assert.Empty(SnipeFilters.RING);
        Assert.Null(SnipeFilters.Next(SnipeFilters.RING, null));
        Assert.Null(SnipeFilters.Next(SnipeFilters.RING, "Scotland cards"));
    }

    [Fact]
    public void RingFiltersAreNamedDistinctlyAndChooseALeagueBeforeAClub()
    {
        Assert.Equal(SnipeFilters.RING.Count, SnipeFilters.RING.Select(filter => filter.Name).Distinct().Count());
        Assert.All(SnipeFilters.RING.Where(filter => filter.Club != null), filter => Assert.NotNull(filter.League));
    }
}
