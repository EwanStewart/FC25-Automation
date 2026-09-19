using Automation.Trading;

namespace Automation.Sbc.Fulfilment;

public sealed record FulfilmentSources(
    Func<int, ChallengeRequirements?> Challenge,
    Func<IReadOnlyList<SquadPlayer>> Club,
    Func<int, CardAttributes?> Card);

public static class FulfilmentProgram
{
    public static void Process(IFulfilmentStore store, Func<int, IReadOnlyList<ApprovalSlot>> slots,
        Func<FulfilmentRun, IMarketAgent> market, Func<FulfilmentRun, ISquadAgent> squad, FulfilmentMode mode,
        FulfilmentSources? sources = null)
    {
        var queued = store.Queued();

        Console.WriteLine(
            $"{queued.Count} approval(s) queued for fulfilment, {FulfilmentModes.Describe(mode)}.");

        foreach (var run in queued) Guarded(store, slots, market, squad, Switched(run, mode), sources);
    }

    private static FulfilmentRun Switched(FulfilmentRun run, FulfilmentMode mode)
    {
        return run with { Mode = mode };
    }

    private static void Guarded(IFulfilmentStore store, Func<int, IReadOnlyList<ApprovalSlot>> slots,
        Func<FulfilmentRun, IMarketAgent> market, Func<FulfilmentRun, ISquadAgent> squad, FulfilmentRun run,
        FulfilmentSources? sources)
    {
        try
        {
            One(store, slots, market, squad, run, sources);
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
        Func<FulfilmentRun, IMarketAgent> market, Func<FulfilmentRun, ISquadAgent> squad, FulfilmentRun run,
        FulfilmentSources? sources)
    {
        var plan = slots(run.ApprovalId);
        SquadBuilder builder = new(squad(run), store);

        Announce(run);

        var owned = Owned(store, builder, run, plan);
        var outcome = owned.Halted
            ? new RunOutcome(FulfilmentState.Failed, owned.Detail)
            : Shop(store, market, builder, run, plan, sources);

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
        SquadBuilder builder, FulfilmentRun run, IReadOnlyList<ApprovalSlot> plan, FulfilmentSources? sources)
    {
        var gaps = store.Gaps(run.Id);
        var bought = new GapBuyer(Buying(market, run), store, Census(sources, run, plan, gaps)).Buy(run, gaps);

        Console.WriteLine($"  market: {Lower(bought.State)} {bought.Detail}".TrimEnd());

        var placed = builder.PlaceBought(run, plan, bought.Gaps);

        Console.WriteLine($"  bought cards: {placed.Detail}");

        return placed.Halted
            ? new RunOutcome(FulfilmentState.Failed, placed.Detail)
            : new RunOutcome(bought.State, bought.Detail);
    }

    private static SquadCensus? Census(FulfilmentSources? sources, FulfilmentRun run,
        IReadOnlyList<ApprovalSlot> plan, IReadOnlyList<GapRecord> gaps)
    {
        return sources is null
            ? null
            : SquadCensus.Of(sources.Challenge(run.ChallengeId), plan, sources.Club(), gaps,
                ChemistryThresholds.Default, TeamLinks.None, sources.Card);
    }

    private static IMarketAgent Buying(Func<FulfilmentRun, IMarketAgent> market, FulfilmentRun run)
    {
        var agent = market(run);

        return run.BuysLive ? agent : new SimulatedMarket(agent);
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
        return run.Mode == FulfilmentMode.Live
            ? detail
            : $"{FulfilmentModes.Simulation(run.Mode)} reached {state}. {detail}".Trim();
    }

    private static string Lower(FulfilmentState state)
    {
        return state.ToString().ToLowerInvariant();
    }

    private static void Announce(FulfilmentRun run)
    {
        Console.WriteLine(
            $"Fulfilment {run.Id} for approval {run.ApprovalId} (challenge {run.ChallengeId}): estimate {run.EstimatedCost}, ceiling {run.SpendCeiling}, {FulfilmentModes.Describe(run.Mode)}.");
    }

    private sealed record RunOutcome(FulfilmentState State, string Detail);
}
