using Automation.Sbc;
using Automation.Sbc.Fulfilment;
using static Automation.Tests.FulfilmentFixtures;

namespace Automation.Tests;

public class MarketPinTests
{
    private static MarketSpecification Wanting(int? nation, int? league, int? club)
    {
        return new MarketSpecification("CB", PlayerQuality.Gold, 75, 82, nation, league, club, null, 1200);
    }

    [Fact]
    public void PinsCountryLeagueAndClubWhenTheGapNamesAllThree()
    {
        var pins = MarketPinner.Ea.For(Wanting(42, 50, 78));

        Assert.Equal("Scotland", pins.Nation);
        Assert.Equal("Scottish Prem (SPFL)", pins.League);
        Assert.Equal("Celtic", pins.Club);
        Assert.Empty(pins.Notes);
    }

    [Fact]
    public void PinsNothingWhenTheGapNamesNothing()
    {
        var pins = MarketPinner.Ea.For(Wanting(null, null, null));

        Assert.Null(pins.Nation);
        Assert.Null(pins.League);
        Assert.Null(pins.Club);
        Assert.False(pins.Pinned);
        Assert.Empty(pins.Notes);
    }

    [Fact]
    public void PinsTheLeagueAClubBelongsToSoTheClubListNarrows()
    {
        var pins = MarketPinner.Ea.For(Wanting(null, null, 1));

        Assert.Equal("Premier League (ENG 1)", pins.League);
        Assert.Equal("Arsenal", pins.Club);
    }

    [Fact]
    public void LeavesAnUnnamedIdUnpinnedAndSaysSo()
    {
        var pins = MarketPinner.Ea.For(Wanting(999999, null, null));

        Assert.Null(pins.Nation);
        Assert.False(pins.Pinned);
        Assert.Contains(pins.Notes, note => note.Contains("999999"));
    }

    [Fact]
    public void KeepsThePinsItCanWhenOneIdHasNoName()
    {
        var pins = MarketPinner.Ea.For(Wanting(42, 999999, null));

        Assert.Equal("Scotland", pins.Nation);
        Assert.Null(pins.League);
        Assert.Single(pins.Notes);
    }

    [Fact]
    public void RefusesAnAmbiguousClubNameThatNoLeaguePins()
    {
        MarketNames names = new("2027", new Dictionary<int, string>(),
            new Dictionary<int, LeagueName>(),
            new Dictionary<int, string> { [1] = "Arsenal", [2] = "Arsenal" });
        MarketPinner pinner = new(names, MarketReference.Ea);
        var pins = pinner.For(Wanting(null, null, 1));

        Assert.Null(pins.Club);
        Assert.Null(pins.League);
        Assert.Contains(pins.Notes, note => note.Contains("Arsenal"));
    }

    [Fact]
    public void CarriesThePinsIntoTheSearchFilter()
    {
        var specification = Wanting(42, 50, 78);
        var filter = MarketFilter.For(specification, 5000, MarketPinner.Ea.For(specification));

        Assert.Equal("Scotland", filter.Nationality);
        Assert.Equal("Scottish Prem (SPFL)", filter.League);
        Assert.Equal("Celtic", filter.Club);
        Assert.True(filter.PinsOptional);
        Assert.Equal("Gold", filter.Quality);
        Assert.Equal("CB", filter.Position);
        Assert.Equal(5000u, filter.MaxBidPrice);
    }

    [Fact]
    public void LeavesTheFilterBroadWhenNothingPins()
    {
        var specification = Wanting(null, null, null);
        var filter = MarketFilter.For(specification, 5000, MarketPinner.Ea.For(specification));

        Assert.Null(filter.Nationality);
        Assert.Null(filter.League);
        Assert.Null(filter.Club);
    }

    [Fact]
    public void SaysWhatItSearchedForSoTheRunRecordReads()
    {
        var specification = Wanting(42, 50, 78);
        var described = MarketFilter.Describe(specification, 5000, MarketPinner.Ea.For(specification));

        Assert.Contains("Gold CB at or under 5000", described);
        Assert.Contains("Celtic", described);
        Assert.Contains("Scotland", described);
    }

    [Fact]
    public void StillRejectsACardFromTheWrongClub()
    {
        var specification = Wanting(42, 50, 78);
        var wrong = Listing("t1", 900, 78) with { NationId = 42, LeagueId = 50, TeamId = 79 };
        var right = Listing("t2", 900, 78) with { NationId = 42, LeagueId = 50, TeamId = 78 };

        Assert.False(MarketCandidateChoice.Matches(specification, wrong));
        Assert.True(MarketCandidateChoice.Matches(specification, right));
    }
}
