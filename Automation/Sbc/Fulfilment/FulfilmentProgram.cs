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
            store.SaveState(run.Id, FulfilmentState.Failed, $"the run was stopped: {exception.Message}");

            throw;
        }
        catch (Exception exception)
        {
            store.SaveState(run.Id, FulfilmentState.Failed, exception.Message);
            Console.WriteLine($"Fulfilment {run.Id} stopped: {exception.Message}");
        }
    }

    private static void One(IFulfilmentStore store, Func<int, IReadOnlyList<ApprovalSlot>> slots,
        Func<FulfilmentRun, IMarketAgent> market, Func<FulfilmentRun, ISquadAgent> squad, FulfilmentRun run)
    {
        Announce(run);

        var bought = new GapBuyer(market(run), store).Buy(run, store.Gaps(run.Id));

        Console.WriteLine($"  buying finished {bought.State}. {bought.Detail}");

        if (bought.State == FulfilmentState.Building) Assemble(store, slots, squad, run, bought);
    }

    private static void Assemble(IFulfilmentStore store, Func<int, IReadOnlyList<ApprovalSlot>> slots,
        Func<FulfilmentRun, ISquadAgent> squad, FulfilmentRun run, BuyingResult bought)
    {
        var built = new SquadBuilder(squad(run), store).Build(run, slots(run.ApprovalId), bought.Gaps);

        store.SaveState(run.Id, GapBuyer.Recorded(run, built.State),
            run.DryRun ? $"dry run reached {built.State}. {built.Detail}".Trim() : built.Detail);
        Console.WriteLine(built.State == FulfilmentState.Built
            ? $"  squad built for approval {run.ApprovalId}. Check it in the app and finish it yourself."
            : $"  squad not built: {built.Detail}");
    }

    private static void Announce(FulfilmentRun run)
    {
        Console.WriteLine(
            $"Fulfilment {run.Id} for approval {run.ApprovalId} (challenge {run.ChallengeId}): estimate {run.EstimatedCost}, ceiling {run.SpendCeiling}, dry run {run.DryRun}.");
    }
}
