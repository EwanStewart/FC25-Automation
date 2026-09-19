using Automation.Sbc;

namespace Automation.Sbc.Fulfilment;

public static class GapAudit
{
    public static IReadOnlyList<GapRecord> Checked(SquadCensus? census, IReadOnlyList<GapRecord> gaps,
        IReadOnlyDictionary<int, CardAttributes> cards)
    {
        return census is null ? gaps : gaps.Select(gap => Judged(census, gaps, cards, gap)).ToList();
    }

    private static GapRecord Judged(SquadCensus census, IReadOnlyList<GapRecord> gaps,
        IReadOnlyDictionary<int, CardAttributes> cards, GapRecord gap)
    {
        var card = Held(gap, cards);

        return card is null ? gap : Verdict(census, gaps, cards, gap, card);
    }

    private static CardAttributes? Held(GapRecord gap, IReadOnlyDictionary<int, CardAttributes> cards)
    {
        return gap.Outcome == GapOutcome.Won && cards.TryGetValue(gap.SlotIndex, out var card) ? card : null;
    }

    private static GapRecord Verdict(SquadCensus census, IReadOnlyList<GapRecord> gaps,
        IReadOnlyDictionary<int, CardAttributes> cards, GapRecord gap, CardAttributes card)
    {
        var faults = Strayed(gap.Specification, card).Concat(Introduced(census, gaps, cards, gap)).ToList();

        return faults.Count == 0
            ? gap
            : gap with
            {
                Outcome = GapOutcome.Mismatched,
                Detail = $"{gap.Detail}, but the card {string.Join("; ", faults)}"
            };
    }

    private static IReadOnlyList<string> Introduced(SquadCensus census, IReadOnlyList<GapRecord> gaps,
        IReadOnlyDictionary<int, CardAttributes> cards, GapRecord gap)
    {
        var planned = Breaches(census, gaps, Without(cards, gap.SlotIndex));
        var actual = Breaches(census, gaps, cards);
        var known = planned.Select(requirement => requirement.Description).ToHashSet();

        return actual.Where(requirement => !known.Contains(requirement.Description))
            .Select(requirement => $"breaks {requirement.Description}").ToList();
    }

    private static IReadOnlyDictionary<int, CardAttributes> Without(IReadOnlyDictionary<int, CardAttributes> cards,
        int slot)
    {
        return cards.Where(entry => entry.Key != slot).ToDictionary(entry => entry.Key, entry => entry.Value);
    }

    private static IReadOnlyList<SquadRequirement> Breaches(SquadCensus census, IReadOnlyList<GapRecord> gaps,
        IReadOnlyDictionary<int, CardAttributes> cards)
    {
        return SquadOutlook.Breaches(census.Challenge, census.Squad, Possibilities(gaps, cards), census.Thresholds,
            census.Links);
    }

    private static IReadOnlyList<GapPossibility> Possibilities(IReadOnlyList<GapRecord> gaps,
        IReadOnlyDictionary<int, CardAttributes> cards)
    {
        return gaps.Select(gap => cards.TryGetValue(gap.SlotIndex, out var card)
            ? Pinned(gap.SlotIndex, card)
            : Wanted(gap)).ToList();
    }

    private static GapPossibility Pinned(int slot, CardAttributes card)
    {
        return new GapPossibility(slot, card.ClubId, card.LeagueId, card.NationId, card.RareFlag, card.Rating,
            card.Rating);
    }

    private static GapPossibility Wanted(GapRecord gap)
    {
        return new GapPossibility(gap.SlotIndex, gap.Specification.ClubId, gap.Specification.LeagueId,
            gap.Specification.NationId, gap.Specification.RareFlag, gap.Specification.MinimumRating,
            gap.Specification.MaximumRating);
    }

    private static IReadOnlyList<string> Strayed(MarketSpecification specification, CardAttributes card)
    {
        List<string> result = [];

        if (specification.ClubId.HasValue && specification.ClubId != card.ClubId)
            result.Add($"plays for club {card.ClubId}, not club {specification.ClubId}");

        if (specification.LeagueId.HasValue && specification.LeagueId != card.LeagueId)
            result.Add($"plays in league {card.LeagueId}, not league {specification.LeagueId}");

        if (specification.NationId.HasValue && specification.NationId != card.NationId)
            result.Add($"comes from nation {card.NationId}, not nation {specification.NationId}");

        if (specification.RareFlag.HasValue && specification.RareFlag != card.RareFlag)
            result.Add($"carries rarity {card.RareFlag}, not rarity {specification.RareFlag}");

        if (card.Rating < specification.MinimumRating || card.Rating > specification.MaximumRating)
            result.Add(
                $"is rated {card.Rating}, outside {specification.MinimumRating} to {specification.MaximumRating}");

        return result;
    }
}
