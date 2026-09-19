namespace Automation.Sbc.Fulfilment;

public sealed record ApprovalSlot(
    int SlotIndex,
    string Position,
    long? ClubPlayerId,
    string PlayerName,
    int Rating,
    bool Owned,
    string? Specification,
    int EstimatedCost);

public sealed record SquadSlotView(int Index, string Position, long ItemId, int AssetId, int Rating, bool Filled);

public sealed record SquadView(int ChallengeId, string Formation, IReadOnlyList<SquadSlotView> Slots);

public sealed record SquadTarget(int SlotIndex, string Position, long ItemId, string Name, string Source);

public interface ISquadAgent
{
    SquadView Open(int challengeId);

    void Place(int slotIndex, SquadTarget target);

    SquadView Read(int challengeId);
}

public static class SquadPlan
{
    public const string FROM_CLUB = "club";
    public const string FROM_MARKET = "bought";
    public const string FROM_DRY_RUN = "simulated";
    public const string UNREADY = "unready";

    public static IReadOnlyList<SquadTarget> Targets(IReadOnlyList<ApprovalSlot> slots,
        IReadOnlyList<GapRecord> gaps)
    {
        return slots.OrderBy(slot => slot.SlotIndex)
            .Select(slot => slot.Owned ? FromClub(slot) : FromGap(slot, gaps)).ToList();
    }

    public static bool Held(string source)
    {
        return source is FROM_CLUB or FROM_MARKET;
    }

    public static IReadOnlyList<SquadTarget> Owned(IReadOnlyList<SquadTarget> targets)
    {
        return targets.Where(target => target.Source == FROM_CLUB).ToList();
    }

    public static IReadOnlyList<SquadTarget> Bought(IReadOnlyList<SquadTarget> targets)
    {
        return targets.Where(target => target.Source is FROM_MARKET or FROM_DRY_RUN).ToList();
    }

    public static IReadOnlyList<SquadTarget> Outstanding(IReadOnlyList<SquadTarget> targets)
    {
        return targets.Where(target => target.Source == UNREADY).ToList();
    }

    public static string Names(IReadOnlyList<SquadTarget> targets)
    {
        return string.Join(", ", targets.Select(target => $"slot {target.SlotIndex}"));
    }

    private static SquadTarget FromClub(ApprovalSlot slot)
    {
        return new SquadTarget(slot.SlotIndex, slot.Position, slot.ClubPlayerId ?? 0, slot.PlayerName, FROM_CLUB);
    }

    private static SquadTarget FromGap(ApprovalSlot slot, IReadOnlyList<GapRecord> gaps)
    {
        var gap = gaps.FirstOrDefault(entry => entry.SlotIndex == slot.SlotIndex);

        return new SquadTarget(slot.SlotIndex, slot.Position, gap?.ItemId ?? 0, slot.PlayerName, Source(gap));
    }

    private static string Source(GapRecord? gap)
    {
        var result = UNREADY;

        if (gap?.Outcome == GapOutcome.Won && gap.ItemId.GetValueOrDefault() > 0) result = FROM_MARKET;
        else if (gap?.Outcome == GapOutcome.Simulated) result = FROM_DRY_RUN;

        return result;
    }
}
