using Automation.Sbc;

namespace Automation.Tests;

public class MarketReferenceTests
{
    [Fact]
    public void CarriesTheWholeShippedReference()
    {
        var reference = MarketReference.Ea;

        Assert.True(reference.Clubs.Count > 800);
        Assert.True(reference.Leagues.Count > 60);
        Assert.True(reference.Nations.Count > 150);
    }

    [Fact]
    public void GivesEveryKnownClubExactlyOneLeague()
    {
        var reference = MarketReference.Ea;

        Assert.All(reference.Clubs, club => Assert.True(reference.LeagueOf(club) > 0));
    }

    [Fact]
    public void PairsAClubWithTheLeagueTheGamePutsItIn()
    {
        var reference = MarketReference.Ea;

        Assert.Equal(13, reference.LeagueOf(8));
        Assert.Equal(53, reference.LeagueOf(243));
        Assert.Equal(19, reference.LeagueOf(36));
        Assert.Equal(16, reference.LeagueOf(217));
    }

    [Fact]
    public void ListsEveryClubThatPlaysInALeague()
    {
        var clubs = MarketReference.Ea.ClubsIn(13);

        Assert.Contains(8, clubs);
        Assert.True(clubs.Count >= 20);
        Assert.All(clubs, club => Assert.Equal(13, MarketReference.Ea.LeagueOf(club)));
    }

    [Fact]
    public void NamesTheCountryALeaguePlaysIn()
    {
        var reference = MarketReference.Ea;

        Assert.Equal(14, reference.HomeNationOf(13));
        Assert.Equal(45, reference.HomeNationOf(53));
        Assert.Equal(21, reference.HomeNationOf(19));
    }

    [Fact]
    public void LeavesTheCountryUnknownWhenTheReferenceHasNoUsableOne()
    {
        Assert.Equal(MarketReference.UNKNOWN_NATION, MarketReference.Ea.HomeNationOf(1014));
        Assert.Equal(MarketReference.UNKNOWN_NATION, MarketReference.Ea.HomeNationOf(999999));
    }

    [Fact]
    public void AcceptsOnlyNationsTheGameShips()
    {
        var reference = MarketReference.Ea;

        Assert.True(reference.KnowsNation(42));
        Assert.True(reference.KnowsNation(14));
        Assert.False(reference.KnowsNation(225));
        Assert.False(reference.KnowsNation(0));
    }

    [Fact]
    public void KeepsIconsAndHeroesOutOfTheBuyableDomain()
    {
        var reference = MarketReference.Ea;

        Assert.False(reference.KnowsClub(ChemistryCalculator.LEGENDS_CLUB_ID));
        Assert.False(reference.KnowsClub(ChemistryCalculator.LEAGUE_HERO_CLUB_ID));
        Assert.False(reference.KnowsClub(ChemistryCalculator.HALL_OF_FUT_CLUB_ID));
        Assert.DoesNotContain(ChemistryCalculator.LEGENDS_LEAGUE_ID, reference.Leagues);
    }

    [Fact]
    public void RanksLeaguesByHowManyClubsCarryThem()
    {
        var ranked = MarketReference.Ea.LeaguesByClubCount;

        Assert.Equal(MarketReference.Ea.Leagues.Count, ranked.Count);
        Assert.True(MarketReference.Ea.ClubsIn(ranked[0]).Count >= MarketReference.Ea.ClubsIn(ranked[1]).Count);
    }
}
