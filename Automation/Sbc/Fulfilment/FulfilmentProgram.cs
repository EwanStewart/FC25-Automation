using Automation.Trading;

namespace Automation.Sbc.Fulfilment;

public static class FulfilmentProgram
{
    public static void Process(IFulfilmentStore store, Func<int, IReadOnlyList<ApprovalSlot>> slots,
        Func<FulfilmentRun, IMarketAgent> market, Func<FulfilmentRun, ISquadAgent> squad, bool live)
    {
        var queued = store.Queued();

        Console.WriteLine($"{queued.Count} approval(s) queued for fulfilment, live buying {live}.");

        foreach (var run in queued) Guarded(store, slots, market, squad, Mode(run, live));
    }

    private static FulfilmentRun Mode(FulfilmentRun run, bool live)
    {
        return run with { DryRun = FulfilmentDefaults.DryRun(live) };
    }

    private static void Guarded(IFulfilmentStore store, Func<int, IReadOnlyList<ApprovalSlot>> slots,
        Func<FulfilmentRun, IMarketAgent> market, Func<FulfilmentRun, ISquadAgent> squad, FulfilmentRun run)
    {
        try
        {
            One(store, slots, market, squad, run);
        }
        catch (RunStoppedException exception)
        {
            store.SaveState(run.Id, FulfilmentDefaults.Recorded(run, FulfilmentState.Failed),
                $"the run was stopped: {exception.Message}");

            throw;
        }
        catch (Exception exception)
        {
            store.SaveState(run.Id, FulfilmentDefaults.Recorded(run, FulfilmentState.Failed), exception.Message);
            Console.WriteLine($"Fulfilment {run.Id} stopped: {exception.Message}");
        }
    }

    private static void One(IFulfilmentStore store, Func<int, IReadOnlyList<ApprovalSlot>> slots,
        Func<FulfilmentRun, IMarketAgent> market, Func<FulfilmentRun, ISquadAgent> squad, FulfilmentRun run)
    {
        var plan = slots(run.ApprovalId);
        SquadBuilder builder = new(squad(run), store);

        Announce(run);

        var owned = Owned(store, builder, run, plan);
        var outcome = owned.Halted
            ? new RunOutcome(FulfilmentState.Failed, owned.Detail)
            : Shop(store, market, builder, run, plan);

        Settle(store, run, plan, outcome);
    }

    private static PlacementReport Owned(IFulfilmentStore store, SquadBuilder builder, FulfilmentRun run,
        IReadOnlyList<ApprovalSlot> plan)
    {
        store.SaveState(run.Id, FulfilmentDefaults.Recorded(run, FulfilmentState.Placing),
            "placing the cards already in the club");

        var report = builder.PlaceOwned(run, plan);

        Console.WriteLine($"  club cards: {report.Detail}");

        return report;
    }

    private static RunOutcome Shop(IFulfilmentStore store, Func<FulfilmentRun, IMarketAgent> market,
        SquadBuilder builder, FulfilmentRun run, IReadOnlyList<ApprovalSlot> plan)
    {
        var bought = new GapBuyer(market(run), store).Buy(run, store.Gaps(run.Id));

        Console.WriteLine($"  market: {Lower(bought.State)} {bought.Detail}".TrimEnd());

        var placed = builder.PlaceBought(run, plan, bought.Gaps);

        Console.WriteLine($"  bought cards: {placed.Detail}");

        return placed.Halted
            ? new RunOutcome(FulfilmentState.Failed, placed.Detail)
            : new RunOutcome(bought.State, bought.Detail);
    }

    private static void Settle(IFulfilmentStore store, FulfilmentRun run, IReadOnlyList<ApprovalSlot> plan,
        RunOutcome outcome)
    {
        var progress = FulfilmentProgress.Of(plan, store.Gaps(run.Id), store.Placements(run.Id));
        var state = Final(outcome, progress);
        var detail = $"{FulfilmentProgress.Describe(progress)}. {outcome.Detail}".Trim();

        store.SaveState(run.Id, FulfilmentDefaults.Recorded(run, state), Reported(run, state, detail));
        Console.WriteLine($"  fulfilment {run.Id} is {Lower(state)}: {detail}");
        Console.WriteLine(state == FulfilmentState.Built
            ? $"  approval {run.ApprovalId} has a full squad. Check it in the app and finish it yourself."
            : $"  approval {run.ApprovalId} needs another run.");
    }

    private static FulfilmentState Final(RunOutcome outcome, RunProgress progress)
    {
        var result = outcome.State;

        if (outcome.State is FulfilmentState.Placing or FulfilmentState.Buying)
            result = FulfilmentProgress.Complete(progress) ? FulfilmentState.Built : FulfilmentState.Buying;

        return result;
    }

    private static string Reported(FulfilmentRun run, FulfilmentState state, string detail)
    {
        return run.DryRun ? $"dry run reached {state}. {detail}".Trim() : detail;
    }

    private static string Lower(FulfilmentState state)
    {
        return state.ToString().ToLowerInvariant();
    }

    private static void Announce(FulfilmentRun run)
    {
        Console.WriteLine(
            $"Fulfilment {run.Id} for approval {run.ApprovalId} (challenge {run.ChallengeId}): estimate {run.EstimatedCost}, ceiling {run.SpendCeiling}, dry run {run.DryRun}.");
    }

    private sealed record RunOutcome(FulfilmentState State, string Detail);
}
