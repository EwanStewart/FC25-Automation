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
        Assert.Equal("first", SnipeFilters.Next(Ring, null).Name);
        Assert.Equal("second", SnipeFilters.Next(Ring, "first").Name);
        Assert.Equal("first", SnipeFilters.Next(Ring, "third").Name);
        Assert.Equal("first", SnipeFilters.Next(Ring, "England").Name);
    }

    [Fact]
    public void RingRunsOmThenScotlandSilversThenBronzesThenPsgAndWraps()
    {
        Assert.Equal("OM silvers", SnipeFilters.Next(SnipeFilters.RING, null).Name);
        Assert.Equal("Scotland silvers", SnipeFilters.Next(SnipeFilters.RING, "OM silvers").Name);
        Assert.Equal("Scotland bronzes", SnipeFilters.Next(SnipeFilters.RING, "Scotland silvers").Name);
        Assert.Equal("PSG silvers", SnipeFilters.Next(SnipeFilters.RING, "Scotland bronzes").Name);
        Assert.Equal("OM silvers", SnipeFilters.Next(SnipeFilters.RING, "PSG silvers").Name);
    }

    [Fact]
    public void RingFiltersAreNamedDistinctlyAndChooseALeagueBeforeAClub()
    {
        Assert.NotEmpty(SnipeFilters.RING);
        Assert.Equal(SnipeFilters.RING.Count, SnipeFilters.RING.Select(filter => filter.Name).Distinct().Count());
        Assert.All(SnipeFilters.RING, filter => Assert.False(string.IsNullOrEmpty(filter.Quality)));
        Assert.All(SnipeFilters.RING.Where(filter => filter.Club != null), filter => Assert.NotNull(filter.League));
    }
}
