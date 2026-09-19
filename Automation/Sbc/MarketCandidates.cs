namespace Automation.Sbc;

public sealed record MarketCandidate(int SlotIndex, MarketSpecification Specification, SquadPlayer Player);

public static class MarketCandidates
{
    private const int SYNTHETIC_ID_BASE = 900000;

    public static IReadOnlyList<MarketCandidate> Generate(ChallengeRequirements challenge,
        IReadOnlyList<string> slotPositions)
    {
        var demands = Demands(challenge);
        var qualities = Qualities(challenge);
        List<MarketCandidate> result = [];

        for (var slot = 0; slot < slotPositions.Count; slot++)
            result.AddRange(ForSlot(slot, slotPositions[slot], demands, qualities, result.Count));

        return result;
    }

    private static IReadOnlyList<MarketCandidate> ForSlot(int slot, string position,
        IReadOnlyList<PlayerFilter?> demands, IReadOnlyList<PlayerQuality> qualities, int issued)
    {
        List<MarketCandidate> result = [];

        foreach (var demand in demands)
        foreach (var quality in qualities)
        foreach (var rating in Ratings(quality))
            result.Add(Build(slot, position, demand, quality, rating, SYNTHETIC_ID_BASE + issued + result.Count));

        return result;
    }

    private static IReadOnlyList<int> Ratings(PlayerQuality quality)
    {
        return [MarketPricing.Floor(quality), MarketPricing.Ceiling(quality)];
    }

    private static MarketCandidate Build(int slot, string position, PlayerFilter? demand, PlayerQuality quality,
        int rating, int identity)
    {
        var nation = demand?.Kind == PlayerFilterKind.Nation ? demand.Value : identity;
        var league = demand?.Kind == PlayerFilterKind.League ? demand.Value : identity;
        var club = demand?.Kind == PlayerFilterKind.Club ? demand.Value : identity;
        var rarity = demand?.Kind == PlayerFilterKind.Rarity ? demand.Value : 0;
        var specification = new MarketSpecification(position, quality, rating, rating,
            demand?.Kind == PlayerFilterKind.Nation ? demand.Value : null,
            demand?.Kind == PlayerFilterKind.League ? demand.Value : null,
            demand?.Kind == PlayerFilterKind.Club ? demand.Value : null,
            demand?.Kind == PlayerFilterKind.Rarity ? demand.Value : null,
            MarketPricing.Estimate(quality, rating, demand?.Kind == PlayerFilterKind.Club,
                demand?.Kind is PlayerFilterKind.Nation or PlayerFilterKind.League,
                demand?.Kind == PlayerFilterKind.Rarity));
        var player = new SquadPlayer(identity, identity, $"Buy: {specification.Describe()}", rating, position,
            [position], club, league, nation, rarity, false, specification.EstimatedCost, false);

        return new MarketCandidate(slot, specification, player);
    }

    private static IReadOnlyList<PlayerFilter?> Demands(ChallengeRequirements challenge)
    {
        List<PlayerFilter?> result = [null];

        result.AddRange(challenge.Requirements.Where(requirement =>
                requirement.Kind is RequirementKind.PlayerCount or RequirementKind.PlayerLevelCount)
            .SelectMany(requirement => requirement.Filters)
            .Where(filter => filter.Kind is PlayerFilterKind.Nation or PlayerFilterKind.League
                or PlayerFilterKind.Club or PlayerFilterKind.Rarity)
            .DistinctBy(filter => (filter.Kind, filter.Value)).Cast<PlayerFilter?>());

        return result;
    }

    private static IReadOnlyList<PlayerQuality> Qualities(ChallengeRequirements challenge)
    {
        PlayerQuality[] all = [PlayerQuality.Bronze, PlayerQuality.Silver, PlayerQuality.Gold];
        var constraints = challenge.Requirements
            .Where(requirement => requirement.Kind == RequirementKind.EveryPlayer)
            .SelectMany(requirement => requirement.Filters
                .Where(filter => filter.Kind == PlayerFilterKind.Quality)
                .Select(filter => (requirement.Comparison, filter.Value))).ToList();

        return all.Where(quality => constraints.All(constraint =>
            SquadAssessor.Compare((int)quality, constraint.Value, constraint.Comparison))).ToList();
    }
}
