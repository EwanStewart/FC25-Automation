using Automation.Sbc;

namespace Automation.Sbc.Fulfilment;

public sealed record CardAttributes(int ClubId, int LeagueId, int NationId, int RareFlag, int Rating);

public sealed record SquadCensus(
    ChallengeRequirements Challenge,
    IReadOnlyList<SquadPlayer> Squad,
    ChemistryThresholds Thresholds,
    TeamLinks Links,
    Func<int, CardAttributes?>? Card = null)
{
    public static SquadCensus? Of(ChallengeRequirements? challenge, IReadOnlyList<ApprovalSlot> slots,
        IReadOnlyList<SquadPlayer> club, IReadOnlyList<GapRecord> gaps, ChemistryThresholds thresholds,
        TeamLinks links, Func<int, CardAttributes?>? card = null)
    {
        var owned = club.GroupBy(player => player.Id).ToDictionary(group => group.Key, group => group.First());
        var ordered = slots.OrderBy(slot => slot.SlotIndex).Select(slot => Player(slot, owned, gaps)).ToList();

        return challenge is null || ordered.Count == 0 || ordered.Any(player => player is null)
            ? null
            : new SquadCensus(challenge, ordered.Select(player => player!).ToList(), thresholds, links, card);
    }

    private static SquadPlayer? Player(ApprovalSlot slot, IReadOnlyDictionary<long, SquadPlayer> owned,
        IReadOnlyList<GapRecord> gaps)
    {
        return slot.Owned ? Held(slot, owned) : Wanted(slot, gaps);
    }

    private static SquadPlayer? Held(ApprovalSlot slot, IReadOnlyDictionary<long, SquadPlayer> owned)
    {
        return slot.ClubPlayerId.HasValue && owned.TryGetValue(slot.ClubPlayerId.Value, out var player)
            ? player
            : null;
    }

    private static SquadPlayer? Wanted(ApprovalSlot slot, IReadOnlyList<GapRecord> gaps)
    {
        var gap = gaps.FirstOrDefault(entry => entry.SlotIndex == slot.SlotIndex);

        return gap is null ? null : Placeholder(slot, gap.Specification);
    }

    private static SquadPlayer Placeholder(ApprovalSlot slot, MarketSpecification specification)
    {
        return new SquadPlayer(0, 0, slot.PlayerName, specification.MinimumRating, slot.Position, [slot.Position],
            specification.ClubId ?? 0, specification.LeagueId ?? 0, specification.NationId ?? 0,
            specification.RareFlag ?? 0, false, 0, false);
    }
}
