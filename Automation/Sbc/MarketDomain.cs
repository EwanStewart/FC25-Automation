namespace Automation.Sbc;

public enum MarketEvidence
{
    Observed,
    Unverified
}

public sealed record MarketAttributes(int ClubId, int LeagueId, int NationId, bool Domestic,
    MarketEvidence Evidence);

public sealed record MarketDomainLimits(int Leagues, int Clubs, int Nations, bool PinClub);

public static class MarketDomain
{
    public const int MINIMUM_LEAGUES = 3;
    public const int MINIMUM_NATIONS = 3;
    public const int MINIMUM_CLUBS = 4;
    public const int MAXIMUM_LEAGUES = 6;
    public const int MAXIMUM_NATIONS = 6;
    public const int MAXIMUM_CLUBS = 8;
    public const int SLACK = 2;

    public static MarketDomainLimits Limits(ChallengeRequirements challenge)
    {
        var leagues = Clamp(Demanded(challenge, RequirementKind.DistinctLeagues) + SLACK, MINIMUM_LEAGUES,
            MAXIMUM_LEAGUES);
        var nations = Clamp(Demanded(challenge, RequirementKind.DistinctNations) + SLACK, MINIMUM_NATIONS,
            MAXIMUM_NATIONS);
        var pinClub = PinsClub(challenge);
        var clubs = pinClub ? Math.Max(Clamp(DemandedClubs(challenge) + SLACK, MINIMUM_CLUBS, MAXIMUM_CLUBS),
            leagues) : 0;

        return new MarketDomainLimits(leagues, clubs, nations, pinClub);
    }

    public static IReadOnlyList<MarketAttributes> Build(ChallengeRequirements challenge,
        IReadOnlyList<SquadPlayer> owned, MarketReference reference)
    {
        var limits = Limits(challenge);
        var leagues = Leagues(challenge, owned, reference, limits.Leagues);
        var nations = Nations(challenge, owned, reference, leagues, limits.Nations);
        var clubs = limits.PinClub ? Clubs(challenge, owned, reference, leagues, limits.Clubs) : [];

        return limits.PinClub
            ? Combine(clubs.Select(club => (club, reference.LeagueOf(club))).ToList(), nations, owned, reference)
            : Combine(leagues.Select(league => (0, league)).ToList(), nations, owned, reference);
    }

    private static IReadOnlyList<MarketAttributes> Combine(IReadOnlyList<(int Club, int League)> places,
        IReadOnlyList<int> nations, IReadOnlyList<SquadPlayer> owned, MarketReference reference)
    {
        var observed = Observed(owned);

        return places.SelectMany(place => nations.Select(nation => new MarketAttributes(place.Club, place.League,
            nation, reference.HomeNationOf(place.League) == nation,
            Evidence(observed, place.Club, place.League, nation)))).ToList();
    }

    private static IReadOnlySet<(int, int, int)> Observed(IReadOnlyList<SquadPlayer> owned)
    {
        return owned.SelectMany(player => new[]
        {
            (player.TeamId, player.LeagueId, player.NationId),
            (0, player.LeagueId, player.NationId)
        }).ToHashSet();
    }

    private static MarketEvidence Evidence(IReadOnlySet<(int, int, int)> observed, int club, int league, int nation)
    {
        return observed.Contains((club, league, nation)) ? MarketEvidence.Observed : MarketEvidence.Unverified;
    }

    private static IReadOnlyList<int> Leagues(ChallengeRequirements challenge, IReadOnlyList<SquadPlayer> owned,
        MarketReference reference, int limit)
    {
        var named = NamedLeagues(challenge, reference);
        var ranked = Ranked(owned.Select(player => player.LeagueId));

        return named.Concat(ranked).Concat(reference.LeaguesByClubCount).Where(league => reference.ClubsIn(league)
            .Count > 0).Distinct().Take(Math.Max(limit, named.Count)).ToList();
    }

    private static IReadOnlyList<int> Nations(ChallengeRequirements challenge, IReadOnlyList<SquadPlayer> owned,
        MarketReference reference, IReadOnlyList<int> leagues, int limit)
    {
        var named = NamedNations(challenge, reference);
        var home = leagues.Select(reference.HomeNationOf);
        var ranked = Ranked(owned.Select(player => player.NationId));

        return named.Concat(home).Concat(ranked).Concat(reference.Nations).Where(reference.KnowsNation).Distinct()
            .Take(Math.Max(limit, named.Count)).ToList();
    }

    private static IReadOnlyList<int> Clubs(ChallengeRequirements challenge, IReadOnlyList<SquadPlayer> owned,
        MarketReference reference, IReadOnlyList<int> leagues, int limit)
    {
        var named = NamedClubs(challenge, reference);
        var queues = leagues.Select(league => Queue(league, named, owned, reference)).ToList();

        return named.Concat(RoundRobin(queues)).Distinct().Take(Math.Max(limit, named.Count)).ToList();
    }

    private static IReadOnlyList<int> Queue(int league, IReadOnlyList<int> named, IReadOnlyList<SquadPlayer> owned,
        MarketReference reference)
    {
        var mine = named.Where(club => reference.LeagueOf(club) == league);
        var ranked = Ranked(owned.Where(player => player.LeagueId == league).Select(player => player.TeamId))
            .Where(reference.KnowsClub);

        return mine.Concat(ranked).Concat(reference.ClubsIn(league)).Distinct().ToList();
    }

    private static IEnumerable<int> RoundRobin(IReadOnlyList<IReadOnlyList<int>> queues)
    {
        var deepest = queues.Count == 0 ? 0 : queues.Max(queue => queue.Count);

        return Enumerable.Range(0, deepest).SelectMany(depth =>
            queues.Where(queue => depth < queue.Count).Select(queue => queue[depth]));
    }

    private static IReadOnlyList<int> Ranked(IEnumerable<int> values)
    {
        return values.Where(value => value > 0).GroupBy(value => value)
            .OrderByDescending(group => group.Count()).ThenBy(group => group.Key).Select(group => group.Key)
            .ToList();
    }

    private static IReadOnlyList<int> NamedLeagues(ChallengeRequirements challenge, MarketReference reference)
    {
        var direct = Named(challenge, PlayerFilterKind.League);
        var throughClubs = Named(challenge, PlayerFilterKind.Club).Select(reference.LeagueOf);

        return direct.Concat(throughClubs).Where(league => reference.ClubsIn(league).Count > 0).Distinct().ToList();
    }

    private static IReadOnlyList<int> NamedClubs(ChallengeRequirements challenge, MarketReference reference)
    {
        return Named(challenge, PlayerFilterKind.Club).Where(reference.KnowsClub).Distinct().ToList();
    }

    private static IReadOnlyList<int> NamedNations(ChallengeRequirements challenge, MarketReference reference)
    {
        return Named(challenge, PlayerFilterKind.Nation).Where(reference.KnowsNation).Distinct().ToList();
    }

    private static IReadOnlyList<int> Named(ChallengeRequirements challenge, PlayerFilterKind kind)
    {
        return challenge.Requirements.SelectMany(requirement => requirement.Filters)
            .Where(filter => filter.Kind == kind)
            .SelectMany(filter => new[] { filter.Value }.Concat(filter.Alternatives ?? [])).ToList();
    }

    private static bool PinsClub(ChallengeRequirements challenge)
    {
        return challenge.Requirements.Any(requirement =>
                   requirement.Kind is RequirementKind.DistinctClubs or RequirementKind.SameClubCount) ||
               challenge.Requirements.SelectMany(requirement => requirement.Filters)
                   .Any(filter => filter.Kind == PlayerFilterKind.Club);
    }

    private static int Demanded(ChallengeRequirements challenge, RequirementKind kind)
    {
        return challenge.Requirements.Where(requirement => requirement.Kind == kind)
            .Select(requirement => requirement.Value).DefaultIfEmpty(0).Max();
    }

    private static int DemandedClubs(ChallengeRequirements challenge)
    {
        var spread = challenge.Requirements
            .Where(requirement => requirement.Kind == RequirementKind.SameClubCount &&
                                  requirement.Comparison == RequirementComparison.Maximum)
            .Select(requirement => Spread(challenge.SquadSize, requirement.Value)).DefaultIfEmpty(0).Max();

        return Math.Max(Demanded(challenge, RequirementKind.DistinctClubs), spread);
    }

    private static int Spread(int squadSize, int perClub)
    {
        return perClub <= 0 ? squadSize : (squadSize + perClub - 1) / perClub;
    }

    private static int Clamp(int value, int minimum, int maximum)
    {
        return Math.Clamp(value, minimum, maximum);
    }
}
