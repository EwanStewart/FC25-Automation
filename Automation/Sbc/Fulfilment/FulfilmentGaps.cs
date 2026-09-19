namespace Automation.Sbc.Fulfilment;

public static class FulfilmentDefaults
{
    public const bool DRY_RUN = true;

    public static bool DryRun(bool liveRequested)
    {
        return !liveRequested;
    }
}

public static class FulfilmentGaps
{
    public static IReadOnlyList<GapRecord> From(SolvedSquad squad)
    {
        return squad.Slots.Where(slot => slot.Gap is not null).OrderBy(slot => slot.Index)
            .Select(slot => new GapRecord(slot.Index, slot.Position, slot.Gap!,
                CardCeiling.For(slot.Gap!.EstimatedCost))).ToList();
    }
}
