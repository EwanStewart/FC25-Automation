using Automation.Sbc;

namespace Automation.Tests;

public class MarketNamesTests
{
    [Fact]
    public void NamesEveryNationTheReferenceCallsBuyable()
    {
        var names = MarketNames.Ea;

        Assert.All(MarketReference.Ea.Nations, nation => Assert.NotNull(names.Nation(nation)));
    }

    [Fact]
    public void GivesTheCountryTheDropdownShows()
    {
        var names = MarketNames.Ea;

        Assert.Equal("England", names.Nation(14));
        Assert.Equal("Scotland", names.Nation(42));
        Assert.Equal("USA", names.Nation(95));
    }

    [Fact]
    public void GivesTheLeagueTheDropdownShowsWithItsAbbreviation()
    {
        var names = MarketNames.Ea;

        Assert.Equal("Premier League (ENG 1)", names.League(13));
        Assert.Equal("Scottish Prem (SPFL)", names.League(50));
        Assert.Equal("Bundesliga (GER 1)", names.League(19));
    }

    [Fact]
    public void GivesTheClubTheDropdownShows()
    {
        var names = MarketNames.Ea;

        Assert.Equal("Arsenal", names.Club(1));
        Assert.Equal("VfB Stuttgart", names.Club(36));
        Assert.Equal("Real Madrid", names.Club(243));
    }

    [Fact]
    public void LeavesAnIdWithNoShippedNameUnnamed()
    {
        var names = MarketNames.Ea;

        Assert.Null(names.Nation(999999));
        Assert.Null(names.League(999999));
        Assert.Null(names.Club(999999));
    }

    [Fact]
    public void CallsAClubNameUniqueOnlyWhenNoOtherClubCarriesIt()
    {
        var names = MarketNames.Ea;

        Assert.False(names.ClubNameIsUnique(1));
        Assert.True(names.ClubNameIsUnique(78));
        Assert.False(names.ClubNameIsUnique(999999));
    }

    [Fact]
    public void KeepsEveryClubNameUniqueInsideItsOwnLeague()
    {
        var reference = MarketReference.Ea;
        var names = MarketNames.Ea;
        var clashes = reference.Leagues
            .SelectMany(league => reference.ClubsIn(league).Select(club => names.Club(club))
                .Where(name => name is not null).GroupBy(name => name).Where(group => group.Count() > 1))
            .ToList();

        Assert.Empty(clashes);
    }
}
