namespace Automation.Sbc;

public sealed record MarketCandidate(int SlotIndex, MarketSpecification Specification, SquadPlayer Player);

public static class MarketCandidates
{
    public const int UNPINNED_CLUB = 0;

    private const int SYNTHETIC_ID_BASE = 900000;

    public static IReadOnlyList<MarketCandidate> Generate(ChallengeRequirements challenge,
        IReadOnlyList<SquadPlayer> owned, IReadOnlyList<string> slotPositions, MarketReference reference)
    {
        var domain = MarketDomain.Build(challenge, owned, reference);
        var observations = MarketObservations.Of(owned);
        var qualities = Qualities(challenge);
        var rarities = Rarities(challenge);
        var ratings = Ratings(challenge);
        List<MarketCandidate> result = [];

        for (var slot = 0; slot < slotPositions.Count; slot++)
            result.AddRange(ForSlot(slot, slotPositions[slot], domain, observations, qualities, ratings, rarities,
                result.Count));

        return result;
    }

    private static IReadOnlyList<MarketCandidate> ForSlot(int slot, string position,
        IReadOnlyList<MarketAttributes> domain, MarketObservations observations,
        IReadOnlyList<PlayerQuality> qualities, Func<PlayerQuality, IReadOnlyList<int>> ratings,
        IReadOnlyList<int> rarities, int issued)
    {
        List<MarketCandidate> result = [];

        foreach (var attributes in domain)
        foreach (var quality in qualities)
        foreach (var rating in ratings(quality))
        foreach (var rarity in rarities)
            result.Add(Build(slot, position, attributes, observations.Evidence(attributes, quality), quality,
                rating, rarity, SYNTHETIC_ID_BASE + issued + result.Count));

        return result;
    }

    private static Func<PlayerQuality, IReadOnlyList<int>> Ratings(ChallengeRequirements challenge)
    {
        var floor = Demanded(challenge);

        return quality => floor is null
            ? [MarketPricing.Floor(quality)]
            : new[] { MarketPricing.Floor(quality), MarketPricing.Ceiling(quality) }.Distinct()
                .Where(rating => rating == MarketPricing.Floor(quality) || rating >= floor).ToList();
    }

    private static int? Demanded(ChallengeRequirements challenge)
    {
        var ratings = challenge.Requirements
            .Where(requirement => requirement.Kind is RequirementKind.SquadRating or RequirementKind.StarRating)
            .Select(requirement => requirement.Value)
            .Concat(challenge.Requirements.SelectMany(requirement => requirement.Filters)
                .Where(filter => filter.Kind is PlayerFilterKind.MinimumRating or PlayerFilterKind.ExactRating
                    or PlayerFilterKind.MaximumRating)
                .Select(filter => filter.Value)).ToList();

        return ratings.Count == 0 ? null : ratings.Max();
    }

    private static MarketCandidate Build(int slot, string position, MarketAttributes attributes,
        MarketEvidence evidence, PlayerQuality quality, int rating, int rarity, int identity)
    {
        var specification = new MarketSpecification(position, quality, rating, rating, attributes.NationId,
            attributes.LeagueId, attributes.ClubId > UNPINNED_CLUB ? attributes.ClubId : null,
            rarity > 0 ? rarity : null,
            MarketPricing.Estimate(quality, rating, attributes.ClubId > UNPINNED_CLUB, true, rarity > 0,
                attributes.Domestic), evidence);
        var player = new SquadPlayer(identity, identity, $"Buy: {specification.Describe()}", rating, position,
            [position], attributes.ClubId, attributes.LeagueId, attributes.NationId, rarity, false,
            specification.EstimatedCost, false);

        return new MarketCandidate(slot, specification, player);
    }

    private static IReadOnlyList<int> Rarities(ChallengeRequirements challenge)
    {
        List<int> result = [0];

        result.AddRange(challenge.Requirements
            .Where(requirement => requirement.Kind is RequirementKind.PlayerCount
                or RequirementKind.PlayerLevelCount)
            .SelectMany(requirement => requirement.Filters)
            .Where(filter => filter.Kind == PlayerFilterKind.Rarity)
            .SelectMany(filter => new[] { filter.Value }.Concat(filter.Alternatives ?? []))
            .Where(rarity => rarity > 0).Distinct());

        return result.Distinct().ToList();
    }

    private static IReadOnlyList<PlayerQuality> Qualities(ChallengeRequirements challenge)
    {
        PlayerQuality[] all = [PlayerQuality.Bronze, PlayerQuality.Silver, PlayerQuality.Gold];
        var constraints = challenge.Requirements
            .Where(requirement => requirement.Kind == RequirementKind.EveryPlayer)
            .SelectMany(requirement => requirement.Filters
                .Where(filter => filter.Kind == PlayerFilterKind.Quality)
                .Select(filter => (requirement.Comparison, filter.Value))).ToList();

        var allowed = all.Where(quality => constraints.All(constraint =>
            SquadAssessor.Compare((int)quality, constraint.Value, constraint.Comparison))).ToList();

        return QualityMatters(challenge) ? allowed : allowed.Take(1).ToList();
    }

    private static bool QualityMatters(ChallengeRequirements challenge)
    {
        return Demanded(challenge) is not null ||
               challenge.Requirements.Any(requirement => requirement.Kind == RequirementKind.PlayerLevelCount) ||
               challenge.Requirements.Where(requirement => requirement.Kind != RequirementKind.EveryPlayer)
                   .SelectMany(requirement => requirement.Filters)
                   .Any(filter => filter.Kind is PlayerFilterKind.Quality or PlayerFilterKind.Level);
    }
}
