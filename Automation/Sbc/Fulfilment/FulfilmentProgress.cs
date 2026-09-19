namespace Automation.Sbc.Fulfilment;

public sealed record RunProgress(int Placed, int Wanted, int Outstanding, string WaitingOn);

public static class FulfilmentProgress
{
    private const string NOTHING_OUTSTANDING = "nothing left to buy";

    public static RunProgress Of(IReadOnlyList<ApprovalSlot> slots, IReadOnlyList<GapRecord> gaps,
        IReadOnlyList<PlacementRecord> placements)
    {
        var wanted = slots.Select(slot => slot.SlotIndex).ToHashSet();
        var placed = placements.Count(placement =>
            wanted.Contains(placement.SlotIndex) && Landed(placement.Outcome));
        var waiting = gaps.Where(gap => !GapProgress.Settled(gap.Outcome)).OrderBy(gap => gap.SlotIndex).ToList();

        return new RunProgress(placed, slots.Count, waiting.Count, Waiting(waiting));
    }

    public static bool Complete(RunProgress progress)
    {
        return progress.Wanted > 0 && progress.Placed >= progress.Wanted && progress.Outstanding == 0;
    }

    public static string Describe(RunProgress progress)
    {
        return
            $"{progress.Placed} of {progress.Wanted} slots placed, {Counted(progress.Outstanding, "gap")} outstanding, {progress.WaitingOn}";
    }

    private static bool Landed(PlacementOutcome outcome)
    {
        return outcome is PlacementOutcome.Placed or PlacementOutcome.Simulated;
    }

    private static string Waiting(IReadOnlyList<GapRecord> gaps)
    {
        return gaps.Count == 0
            ? NOTHING_OUTSTANDING
            : $"waiting on {string.Join("; ", gaps.GroupBy(gap => Phrase(gap.Outcome)).Select(Named))}";
    }

    private static string Named(IGrouping<string, GapRecord> group)
    {
        var slots = string.Join(", ", group.Select(gap => gap.SlotIndex));

        return $"{(group.Count() == 1 ? "slot" : "slots")} {slots} {group.Key}";
    }

    private static string Counted(int count, string word)
    {
        return $"{count} {word}{(count == 1 ? string.Empty : "s")}";
    }

    private static string Phrase(GapOutcome outcome)
    {
        return outcome switch
        {
            GapOutcome.Bidding => "bidding until the auction ends",
            GapOutcome.Attempting => "carrying a bid that has not been read back",
            GapOutcome.Unresolved => "stalled and needing a look",
            GapOutcome.TooExpensive => "priced above the card ceiling",
            GapOutcome.NotFound => "not on the market yet",
            GapOutcome.Outbid => "outbid and due another go",
            GapOutcome.Expired => "lost at auction and due another go",
            _ => "still to buy"
        };
    }
}
