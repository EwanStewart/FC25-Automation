namespace Automation.Sbc;

public static class MarketRelaxation
{
    public static IReadOnlyList<SquadSlot> Relax(ChallengeRequirements challenge, IReadOnlyList<SquadSlot> slots,
        SolveOptions options, MarketReference reference)
    {
        var squad = slots.Select(slot => slot.Player).ToList();
        var widened = Widened(challenge, squad, Pinned(slots), options);

        return slots.Select(slot => Rebuilt(slot, widened, reference)).ToList();
    }

    private static IReadOnlyList<GapPossibility> Pinned(IReadOnlyList<SquadSlot> slots)
    {
        return slots.Where(slot => slot.Gap is not null).Select(slot => new GapPossibility(slot.Index,
            slot.Player.TeamId, slot.Player.LeagueId, slot.Player.NationId, slot.Gap!.RareFlag,
            slot.Gap.MinimumRating, slot.Gap.MaximumRating)).ToList();
    }

    private static IReadOnlyList<GapPossibility> Widened(ChallengeRequirements challenge,
        IReadOnlyList<SquadPlayer> squad, IReadOnlyList<GapPossibility> pinned, SolveOptions options)
    {
        var result = pinned.ToList();

        for (var index = 0; index < result.Count; index++)
        {
            Unpin(challenge, squad, result, index, options);
            Widen(challenge, squad, result, index, options);
        }

        return result;
    }

    private static void Unpin(ChallengeRequirements challenge, IReadOnlyList<SquadPlayer> squad,
        List<GapPossibility> gaps, int index, SolveOptions options)
    {
        Tried(challenge, squad, gaps, index, gap => gap with { ClubId = null }, options);

        if (gaps[index].ClubId is null)
            Tried(challenge, squad, gaps, index, gap => gap with { LeagueId = null }, options);

        Tried(challenge, squad, gaps, index, gap => gap with { NationId = null }, options);
    }

    private static void Widen(ChallengeRequirements challenge, IReadOnlyList<SquadPlayer> squad,
        List<GapPossibility> gaps, int index, SolveOptions options)
    {
        var quality = QualityBand.Of(squad[gaps[index].SlotIndex].Rating);

        Lower(challenge, squad, gaps, index, MarketPricing.Floor(quality), options);
        Raise(challenge, squad, gaps, index, MarketPricing.Ceiling(quality), options);
    }

    private static void Lower(ChallengeRequirements challenge, IReadOnlyList<SquadPlayer> squad,
        List<GapPossibility> gaps, int index, int floor, SolveOptions options)
    {
        var rating = floor;
        var settled = false;

        while (!settled && rating < gaps[index].MinimumRating)
        {
            var wanted = rating;

            settled = Tried(challenge, squad, gaps, index, gap => gap with { MinimumRating = wanted }, options);
            rating++;
        }
    }

    private static void Raise(ChallengeRequirements challenge, IReadOnlyList<SquadPlayer> squad,
        List<GapPossibility> gaps, int index, int ceiling, SolveOptions options)
    {
        var rating = ceiling;
        var settled = false;

        while (!settled && rating > gaps[index].MaximumRating)
        {
            var wanted = rating;

            settled = Tried(challenge, squad, gaps, index, gap => gap with { MaximumRating = wanted }, options);
            rating--;
        }
    }

    private static bool Tried(ChallengeRequirements challenge, IReadOnlyList<SquadPlayer> squad,
        List<GapPossibility> gaps, int index, Func<GapPossibility, GapPossibility> change, SolveOptions options)
    {
        var previous = gaps[index];

        gaps[index] = change(previous);

        var safe = Safe(challenge, squad, gaps, options);

        if (!safe) gaps[index] = previous;

        return safe;
    }

    private static bool Safe(ChallengeRequirements challenge, IReadOnlyList<SquadPlayer> squad,
        IReadOnlyList<GapPossibility> gaps, SolveOptions options)
    {
        return SquadOutlook.Breaches(challenge, squad, gaps, options.Thresholds, options.Links).Count == 0;
    }

    private static SquadSlot Rebuilt(SquadSlot slot, IReadOnlyList<GapPossibility> widened,
        MarketReference reference)
    {
        var gap = widened.FirstOrDefault(entry => entry.SlotIndex == slot.Index);

        return slot.Gap is null || gap is null ? slot : Respecified(slot, gap, reference);
    }

    private static SquadSlot Respecified(SquadSlot slot, GapPossibility gap, MarketReference reference)
    {
        var specification = slot.Gap! with
        {
            MinimumRating = gap.MinimumRating,
            MaximumRating = gap.MaximumRating,
            NationId = gap.NationId,
            LeagueId = gap.LeagueId,
            ClubId = gap.ClubId,
            EstimatedCost = Cost(slot.Gap, gap, reference)
        };

        return slot with
        {
            Gap = specification, Player = slot.Player with { Name = $"Buy: {specification.Describe()}" }
        };
    }

    private static int Cost(MarketSpecification specification, GapPossibility gap, MarketReference reference)
    {
        return MarketPricing.Estimate(specification.Quality, gap.MinimumRating, gap.ClubId.HasValue,
            gap.NationId.HasValue || gap.LeagueId.HasValue, gap.RareFlag.HasValue, Domestic(gap, reference));
    }

    private static bool Domestic(GapPossibility gap, MarketReference reference)
    {
        return !gap.LeagueId.HasValue || !gap.NationId.HasValue ||
               reference.HomeNationOf(gap.LeagueId.Value) == gap.NationId.Value;
    }
}
