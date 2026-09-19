namespace Automation.Sbc.Fulfilment;

public sealed record BuildingResult(FulfilmentState State, string Detail);

public sealed class SquadBuilder
{
    private readonly ISquadAgent squad_;
    private readonly IFulfilmentStore store_;

    public SquadBuilder(ISquadAgent squad, IFulfilmentStore store)
    {
        squad_ = squad;
        store_ = store;
    }

    public BuildingResult Build(FulfilmentRun run, IReadOnlyList<ApprovalSlot> slots,
        IReadOnlyList<GapRecord> gaps)
    {
        var targets = SquadPlan.Targets(slots, gaps);

        return SquadPlan.Ready(targets)
            ? Fill(run, targets)
            : new BuildingResult(FulfilmentState.Failed, $"no card in hand for {SquadPlan.Unready(targets)}");
    }

    private BuildingResult Fill(FulfilmentRun run, IReadOnlyList<SquadTarget> targets)
    {
        var view = squad_.Open(run.ChallengeId);
        var halt = string.Empty;

        foreach (var target in targets)
        {
            var placement = Fill(run, target, ref view);

            store_.SavePlacement(run.Id, placement);

            if (placement.Outcome != PlacementOutcome.Placed && placement.Outcome != PlacementOutcome.Simulated)
            {
                halt = $"slot {target.SlotIndex} could not be filled: {placement.Detail}";
                break;
            }
        }

        return new BuildingResult(halt.Length > 0 ? FulfilmentState.Failed : FulfilmentState.Built, halt);
    }

    private PlacementRecord Fill(FulfilmentRun run, SquadTarget target, ref SquadView view)
    {
        PlacementRecord result;

        if (Holds(view, target)) result = Record(target, PlacementOutcome.Placed, false, "already in the squad");
        else if (run.DryRun) result = Record(target, PlacementOutcome.Simulated, true, "would place");
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
