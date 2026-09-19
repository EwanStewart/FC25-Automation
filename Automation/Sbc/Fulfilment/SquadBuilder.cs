namespace Automation.Sbc.Fulfilment;

public sealed record PlacementReport(int Placed, int Wanted, string Detail, bool Halted);

public sealed class SquadBuilder
{
    private const string NOTHING_TO_PLACE = "no card waiting for a slot";

    private readonly ISquadAgent squad_;
    private readonly IFulfilmentStore store_;

    public SquadBuilder(ISquadAgent squad, IFulfilmentStore store)
    {
        squad_ = squad;
        store_ = store;
    }

    public PlacementReport PlaceOwned(FulfilmentRun run, IReadOnlyList<ApprovalSlot> slots)
    {
        return Fill(run, Outstanding(run, SquadPlan.Owned(SquadPlan.Targets(slots, []))));
    }

    public PlacementReport PlaceBought(FulfilmentRun run, IReadOnlyList<ApprovalSlot> slots,
        IReadOnlyList<GapRecord> gaps)
    {
        return Fill(run, Outstanding(run, SquadPlan.Bought(SquadPlan.Targets(slots, gaps))));
    }

    private IReadOnlyList<SquadTarget> Outstanding(FulfilmentRun run, IReadOnlyList<SquadTarget> targets)
    {
        var done = store_.Placements(run.Id).Where(placement => placement.Outcome == PlacementOutcome.Placed)
            .Select(placement => placement.SlotIndex).ToHashSet();

        return targets.Where(target => !done.Contains(target.SlotIndex)).ToList();
    }

    private PlacementReport Fill(FulfilmentRun run, IReadOnlyList<SquadTarget> targets)
    {
        return targets.Count == 0
            ? new PlacementReport(0, 0, NOTHING_TO_PLACE, false)
            : Opened(run, targets);
    }

    private PlacementReport Opened(FulfilmentRun run, IReadOnlyList<SquadTarget> targets)
    {
        var view = squad_.Open(run.ChallengeId);
        var halt = string.Empty;
        var placed = 0;

        foreach (var target in targets)
        {
            var placement = Attempt(run, target, ref view);

            store_.SavePlacement(run.Id, placement);

            if (Landed(placement.Outcome)) placed++;
            else halt = $"slot {target.SlotIndex} would not take {target.Name}: {placement.Detail}";

            if (halt.Length > 0) break;
        }

        return new PlacementReport(placed, targets.Count, Summary(placed, targets.Count, halt), halt.Length > 0);
    }

    private PlacementRecord Attempt(FulfilmentRun run, SquadTarget target, ref SquadView view)
    {
        PlacementRecord result;

        if (Holds(view, target)) result = Record(target, PlacementOutcome.Placed, false, "already in the squad");
        else if (!Live(run, target)) result = Record(target, PlacementOutcome.Simulated, true, "would place");
        else
        {
            squad_.Place(target.SlotIndex, target);
            view = squad_.Read(run.ChallengeId);
            result = Holds(view, target)
                ? Record(target, PlacementOutcome.Placed, false, "verified on the squad")
                : Record(target, PlacementOutcome.Missing, false, "the slot did not take the card");
        }

        return result;
    }

    private static bool Live(FulfilmentRun run, SquadTarget target)
    {
        return run.PlacesLive && SquadPlan.Held(target.Source);
    }

    private static string Summary(int placed, int wanted, string halt)
    {
        return halt.Length > 0 ? halt : $"{placed} of {wanted} placed";
    }

    private static bool Landed(PlacementOutcome outcome)
    {
        return outcome is PlacementOutcome.Placed or PlacementOutcome.Simulated;
    }

    private static PlacementRecord Record(SquadTarget target, PlacementOutcome outcome, bool simulated,
        string detail)
    {
        return new PlacementRecord(target.SlotIndex, target.Position, target.Source,
            target.Source == SquadPlan.FROM_CLUB ? target.ItemId : null, target.Name, outcome, simulated, detail);
    }

    private static bool Holds(SquadView view, SquadTarget target)
    {
        var slot = view.Slots.FirstOrDefault(entry => entry.Index == target.SlotIndex);

        return slot is not null && slot.Filled && slot.ItemId == target.ItemId && target.ItemId > 0;
    }
}
