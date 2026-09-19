using Automation.Sbc;

namespace Automation.Tests;

public class MarketDomainTests
{
    private static ChallengeRequirements Challenge(params SquadRequirement[] requirements)
    {
        return new ChallengeRequirements(1, 1, "Test", "f442", requirements);
    }

    private static SquadRequirement Distinct(RequirementKind kind, RequirementComparison comparison, int value)
    {
        return new SquadRequirement(kind, comparison, value, [], $"{kind} {value}");
    }

    private static SquadPlayer Owned(int id, int club, int league, int nation)
    {
        return new SquadPlayer(id, id, $"Owned {id}", 70, "CM", ["CM"], club, league, nation, 0, true, 300, true);
    }

    private static IReadOnlyList<SquadPlayer> Squad()
    {
        return [Owned(1, 8, 13, 14), Owned(2, 243, 53, 45), Owned(3, 36, 19, 21)];
    }

    [Fact]
    public void SizesTheLeaguePoolFromTheLeagueCountTheChallengeAsksFor()
    {
        var limits = MarketDomain.Limits(Challenge(Distinct(RequirementKind.DistinctLeagues,
            RequirementComparison.Exact, 4)));

        Assert.Equal(6, limits.Leagues);
    }

    [Fact]
    public void SizesTheNationPoolFromTheNationCountTheChallengeAsksFor()
    {
        var limits = MarketDomain.Limits(Challenge(Distinct(RequirementKind.DistinctNations,
            RequirementComparison.Exact, 2)));

        Assert.Equal(4, limits.Nations);
    }

    [Fact]
    public void KeepsAModestPoolWhenTheChallengeNamesNoCounts()
    {
        var limits = MarketDomain.Limits(Challenge());

        Assert.Equal(MarketDomain.MINIMUM_LEAGUES, limits.Leagues);
        Assert.Equal(MarketDomain.MINIMUM_NATIONS, limits.Nations);
        Assert.False(limits.PinClub);
    }

    [Fact]
    public void PinsTheClubOnlyWhenTheChallengeConstrainsClubs()
    {
        Assert.False(MarketDomain.Limits(Challenge()).PinClub);
        Assert.True(MarketDomain.Limits(Challenge(Distinct(RequirementKind.DistinctClubs,
            RequirementComparison.Maximum, 3))).PinClub);
        Assert.True(MarketDomain.Limits(Challenge(Distinct(RequirementKind.SameClubCount,
            RequirementComparison.Maximum, 2))).PinClub);
    }

    [Fact]
    public void OffersEnoughClubsToSpreadASquadUnderASameClubCeiling()
    {
        var limits = MarketDomain.Limits(Challenge(
            new SquadRequirement(RequirementKind.PlayerCount, RequirementComparison.Exact, 11, [], "size"),
            Distinct(RequirementKind.SameClubCount, RequirementComparison.Maximum, 2)));

        Assert.True(limits.Clubs >= 6);
    }

    [Fact]
    public void EveryClubInTheDomainCarriesItsOwnRealLeague()
    {
        var domain = MarketDomain.Build(Challenge(Distinct(RequirementKind.DistinctClubs,
            RequirementComparison.Maximum, 3)), Squad(), MarketReference.Ea);

        Assert.NotEmpty(domain);
        Assert.All(domain, entry => Assert.Equal(MarketReference.Ea.LeagueOf(entry.ClubId), entry.LeagueId));
    }

    [Fact]
    public void LeavesTheClubUnpinnedWhenNoRequirementCountsClubs()
    {
        var domain = MarketDomain.Build(Challenge(Distinct(RequirementKind.DistinctLeagues,
            RequirementComparison.Exact, 3)), Squad(), MarketReference.Ea);

        Assert.All(domain, entry => Assert.Equal(0, entry.ClubId));
        Assert.All(domain, entry => Assert.Contains(entry.LeagueId, MarketReference.Ea.Leagues));
    }

    [Fact]
    public void EveryNationInTheDomainIsOneTheGameShips()
    {
        var domain = MarketDomain.Build(Challenge(Distinct(RequirementKind.DistinctNations,
            RequirementComparison.Exact, 5)), Squad(), MarketReference.Ea);

        Assert.All(domain, entry => Assert.True(MarketReference.Ea.KnowsNation(entry.NationId)));
    }

    [Fact]
    public void HoldsTheLeagueAndNationACountConstraintNeeds()
    {
        var domain = MarketDomain.Build(Challenge(Distinct(RequirementKind.DistinctLeagues,
            RequirementComparison.Exact, 4), Distinct(RequirementKind.DistinctNations,
            RequirementComparison.Exact, 5)), Squad(), MarketReference.Ea);

        Assert.True(domain.Select(entry => entry.LeagueId).Distinct().Count() >= 4);
        Assert.True(domain.Select(entry => entry.NationId).Distinct().Count() >= 5);
    }

    [Fact]
    public void AlwaysHoldsALeagueAndNationTheChallengeNamesByHand()
    {
        var scotland = new SquadRequirement(RequirementKind.PlayerCount, RequirementComparison.Minimum, 1,
            [new PlayerFilter(PlayerFilterKind.Nation, 42, "Scotland")], "Scotland");
        var portugal = new SquadRequirement(RequirementKind.PlayerCount, RequirementComparison.Minimum, 2,
            [new PlayerFilter(PlayerFilterKind.League, 308, "Liga Portugal")], "Liga Portugal");

        var domain = MarketDomain.Build(Challenge(scotland, portugal), Squad(), MarketReference.Ea);

        Assert.Contains(domain, entry => entry.NationId == 42);
        Assert.Contains(domain, entry => entry.LeagueId == 308);
    }

    [Fact]
    public void AlwaysHoldsAClubTheChallengeNamesByHand()
    {
        var derby = new SquadRequirement(RequirementKind.PlayerCount, RequirementComparison.Minimum, 1,
            [new PlayerFilter(PlayerFilterKind.Club, 73, "Celtic or Rangers", [219])], "Celtic or Rangers");

        var domain = MarketDomain.Build(Challenge(derby), Squad(), MarketReference.Ea);

        Assert.Contains(domain, entry => entry.ClubId == 73);
        Assert.Contains(domain, entry => entry.ClubId == 219);
    }

    [Fact]
    public void PrefersTheLeaguesTheOwnedSquadAlreadyPlaysIn()
    {
        var domain = MarketDomain.Build(Challenge(), Squad(), MarketReference.Ea);
        var leagues = domain.Select(entry => entry.LeagueId).Distinct().ToList();

        Assert.Contains(13, leagues);
        Assert.Contains(53, leagues);
        Assert.Contains(19, leagues);
    }

    [Fact]
    public void MarksALeagueAndNationPairingTheOwnedSquadProvesAsObserved()
    {
        var domain = MarketDomain.Build(Challenge(), Squad(), MarketReference.Ea);

        Assert.Contains(domain, entry =>
            entry.LeagueId == 13 && entry.NationId == 14 && entry.Evidence == MarketEvidence.Observed);
    }

    [Fact]
    public void MarksAPairingNothingProvesAsUnverified()
    {
        var domain = MarketDomain.Build(Challenge(), Squad(), MarketReference.Ea);

        Assert.Contains(domain, entry =>
            entry.LeagueId == 13 && entry.NationId == 45 && entry.Evidence == MarketEvidence.Unverified);
    }

    [Fact]
    public void FlagsAPlayerFromTheLeaguesOwnCountryAsDomestic()
    {
        var domain = MarketDomain.Build(Challenge(), Squad(), MarketReference.Ea);

        Assert.All(domain.Where(entry => entry.Domestic),
            entry => Assert.Equal(MarketReference.Ea.HomeNationOf(entry.LeagueId), entry.NationId));
        Assert.Contains(domain, entry => entry.Domestic);
    }

    [Fact]
    public void StaysSmallEnoughToSolve()
    {
        var domain = MarketDomain.Build(Challenge(Distinct(RequirementKind.DistinctLeagues,
                RequirementComparison.Exact, 5), Distinct(RequirementKind.DistinctNations,
                RequirementComparison.Exact, 6), Distinct(RequirementKind.SameClubCount,
                RequirementComparison.Maximum, 2)), Squad(), MarketReference.Ea);

        Assert.True(domain.Count <= 60);
    }
}
