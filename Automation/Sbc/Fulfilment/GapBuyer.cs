using Automation.Trading;

namespace Automation.Sbc.Fulfilment;

public sealed record BuyingResult(FulfilmentState State, string Detail, IReadOnlyList<GapRecord> Gaps);

public sealed class GapBuyer
{
    private const string CEILING_DETAIL = "the run ceiling leaves too little to buy another card";

    private readonly IMarketAgent market_;
    private readonly IFulfilmentStore store_;

    public GapBuyer(IMarketAgent market, IFulfilmentStore store)
    {
        market_ = market;
        store_ = store;
    }

    public BuyingResult Buy(FulfilmentRun run, IReadOnlyList<GapRecord> gaps)
    {
        var resolved = Resolved(run, gaps);
        var stalled = resolved.FirstOrDefault(gap => gap.Outcome == GapOutcome.Unresolved);
        var result = stalled is null
            ? Work(run, resolved)
            : new BuyingResult(FulfilmentState.Failed, Names(stalled), resolved);

        store_.SaveState(run.Id, Recorded(run, result.State), Reported(run, result));

        return result;
    }

    public static FulfilmentState Recorded(FulfilmentRun run, FulfilmentState state)
    {
        return run.DryRun ? FulfilmentState.Pending : state;
    }

    private static string Reported(FulfilmentRun run, BuyingResult result)
    {
        return run.DryRun ? $"dry run reached {result.State}. {result.Detail}".Trim() : result.Detail;
    }

    private IReadOnlyList<GapRecord> Resolved(FulfilmentRun run, IReadOnlyList<GapRecord> gaps)
    {
        var states = gaps.Any(gap => gap.Outcome is GapOutcome.Attempting or GapOutcome.Bidding)
            ? market_.Standing()
            : [];
        var result = gaps.Select(gap => StandingBids.Resolve(gap, states)).OrderBy(gap => gap.SlotIndex).ToList();

        foreach (var gap in result) store_.SaveGap(run.Id, gap);

        return result;
    }

    private BuyingResult Work(FulfilmentRun run, IReadOnlyList<GapRecord> gaps)
    {
        Dictionary<int, GapRecord> current = gaps.ToDictionary(gap => gap.SlotIndex);
        SpendLedger ledger = new(run.SpendCeiling, StandingBids.Exposure(gaps));
        var halt = string.Empty;
        var state = FulfilmentState.Aborted;

        foreach (var gap in gaps.Where(entry => GapProgress.Retryable(entry.Outcome)))
        {
            var step = Step(run, gap, ledger);

            current[gap.SlotIndex] = step.Gap;
            store_.SaveGap(run.Id, step.Gap);

            if (step.Halt.Length > 0)
            {
                halt = step.Halt;
                state = step.State;
                break;
            }
        }

        var settled = current.Values.OrderBy(gap => gap.SlotIndex).ToList();

        return new BuyingResult(halt.Length > 0 ? state : Reached(settled), halt, settled);
    }

    private GapStep Step(FulfilmentRun run, GapRecord gap, SpendLedger ledger)
    {
        var affordable = CardCeiling.Affordable(gap.CardCeiling, ledger, gap.SlotIndex);

        return affordable < CardCeiling.MINIMUM_COINS
            ? new GapStep(gap with { Detail = CEILING_DETAIL }, CEILING_DETAIL, FulfilmentState.Aborted)
            : Attempt(run, gap, (uint)affordable, ledger);
    }

    private GapStep Attempt(FulfilmentRun run, GapRecord gap, uint ceiling, SpendLedger ledger)
    {
        var search = market_.Search(gap.Specification, ceiling);
        var searched = gap with { Searched = search.Description, CandidatesSeen = search.Listings.Count };
        var choice = MarketCandidateChoice.Best(gap.Specification, search.Listings, ceiling);
        GapStep result;

        if (choice is null) result = new GapStep(Missed(searched, search, ceiling), string.Empty,
            FulfilmentState.Buying);
        else if (!ledger.Allows(gap.SlotIndex, choice.Price))
            result = new GapStep(searched with { Detail = CEILING_DETAIL }, CEILING_DETAIL,
                FulfilmentState.Aborted);
        else result = Take(run, searched, choice, ledger);

        return result;
    }

    private GapStep Take(FulfilmentRun run, GapRecord gap, MarketChoice choice, SpendLedger ledger)
    {
        var marked = Marked(gap, choice);

        ledger.Commit(gap.SlotIndex, choice.Price);

        return run.DryRun
            ? new GapStep(marked with { Outcome = GapOutcome.Simulated, Simulated = true,
                Detail = $"would bid {choice.Price}" }, string.Empty, FulfilmentState.Buying)
            : Place(run, marked, choice);
    }

    private GapStep Place(FulfilmentRun run, GapRecord gap, MarketChoice choice)
    {
        store_.SaveGap(run.Id, gap with { Outcome = GapOutcome.Attempting, Simulated = false });

        var receipt = market_.Bid(choice, choice.Price);
        var placed = gap with { Outcome = Landed(receipt), Simulated = false, Detail = receipt.Detail };

        return new GapStep(placed, placed.Outcome == GapOutcome.Unresolved ? Names(placed) : string.Empty,
            FulfilmentState.Failed);
    }

    private static GapRecord Marked(GapRecord gap, MarketChoice choice)
    {
        return gap with
        {
            TradeId = choice.Listing.TradeId,
            ItemId = choice.Listing.ItemId,
            AssetId = choice.Listing.AssetId,
            BidAmount = (int)choice.Price,
            FinalPrice = null
        };
    }

    private static GapOutcome Landed(BidReceipt receipt)
    {
        return receipt.Placed
            ? receipt.Outcome switch
            {
                BidOutcome.Registered => GapOutcome.Bidding,
                BidOutcome.Overtaken => GapOutcome.Outbid,
                _ => GapOutcome.Unresolved
            }
            : GapOutcome.TooExpensive;
    }

    private static GapRecord Missed(GapRecord gap, MarketSearch search, uint ceiling)
    {
        var fitting = search.Listings.Any(listing => MarketCandidateChoice.Matches(gap.Specification, listing));

        return gap with
        {
            Outcome = fitting ? GapOutcome.TooExpensive : GapOutcome.NotFound,
            Detail = fitting ? $"nothing matching sat at or under {ceiling}" : $"no card matched under {ceiling}"
        };
    }

    private static FulfilmentState Reached(IReadOnlyList<GapRecord> gaps)
    {
        return gaps.All(gap => GapProgress.Settled(gap.Outcome)) ? FulfilmentState.Building : FulfilmentState.Buying;
    }

    private static string Names(GapRecord gap)
    {
        return $"slot {gap.SlotIndex}: {gap.Detail}";
    }

    private sealed record GapStep(GapRecord Gap, string Halt, FulfilmentState State);
}
