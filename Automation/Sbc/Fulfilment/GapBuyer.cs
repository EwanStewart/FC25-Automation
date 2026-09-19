using Automation.Trading;

namespace Automation.Sbc.Fulfilment;

public sealed record BuyingResult(FulfilmentState State, string Detail, IReadOnlyList<GapRecord> Gaps);

public sealed class GapBuyer
{
    private const string CEILING_DETAIL = "the run ceiling leaves too little to buy another card";
    private const string UNWATCHED_DETAIL = "no card on the page could be watched";
    private const int POLL_MS = 1000;
    private const int MAX_POLLS = 240;

    private readonly IMarketAgent market_;
    private readonly IFulfilmentStore store_;
    private readonly SquadCensus? census_;
    private readonly Dictionary<string, AuctionListing> seen_ = new();

    public GapBuyer(IMarketAgent market, IFulfilmentStore store, SquadCensus? census = null)
    {
        market_ = market;
        store_ = store;
        census_ = census;
    }

    public BuyingResult Buy(FulfilmentRun run, IReadOnlyList<GapRecord> gaps)
    {
        var resolved = Audited(run, Resolved(run, gaps));
        var stalled = resolved.FirstOrDefault(gap => Stuck(gap.Outcome));
        var result = stalled is null
            ? Worked(run, resolved)
            : new BuyingResult(FulfilmentState.Failed, Names(stalled), resolved);

        store_.SaveState(run.Id, FulfilmentDefaults.Recorded(run, result.State), Reported(run, result));

        return result;
    }

    private static bool Stuck(GapOutcome outcome)
    {
        return outcome is GapOutcome.Unresolved or GapOutcome.Mismatched;
    }

    private BuyingResult Worked(FulfilmentRun run, IReadOnlyList<GapRecord> gaps)
    {
        var bought = Work(run, gaps);
        var audited = Audited(run, bought.Gaps);
        var broken = audited.FirstOrDefault(gap => gap.Outcome == GapOutcome.Mismatched);

        return broken is null
            ? bought with { Gaps = audited }
            : new BuyingResult(FulfilmentState.Failed, Names(broken), audited);
    }

    private IReadOnlyList<GapRecord> Audited(FulfilmentRun run, IReadOnlyList<GapRecord> gaps)
    {
        var judged = GapAudit.Checked(census_, gaps, Cards(gaps));

        foreach (var gap in judged.Where(gap => gap.Outcome == GapOutcome.Mismatched)) store_.SaveGap(run.Id, gap);

        return judged;
    }

    private IReadOnlyDictionary<int, CardAttributes> Cards(IReadOnlyList<GapRecord> gaps)
    {
        Dictionary<int, CardAttributes> result = new();

        foreach (var gap in gaps.Where(gap => gap.Outcome == GapOutcome.Won))
        {
            var card = Card(gap);

            if (card is not null) result[gap.SlotIndex] = card;
        }

        return result;
    }

    private CardAttributes? Card(GapRecord gap)
    {
        var catalogued = gap.AssetId.HasValue ? census_?.Card?.Invoke(gap.AssetId.Value) : null;

        return catalogued ?? Listed(gap);
    }

    private CardAttributes? Listed(GapRecord gap)
    {
        return gap.TradeId is not null && seen_.TryGetValue(gap.TradeId, out var listing)
            ? new CardAttributes(listing.TeamId, listing.LeagueId, listing.NationId, listing.RareFlag,
                listing.Rating)
            : null;
    }

    private void Remember(IReadOnlyList<AuctionListing> listings)
    {
        foreach (var listing in listings) seen_[listing.TradeId] = listing;
    }

    private static string Reported(FulfilmentRun run, BuyingResult result)
    {
        return run.BuysLive
            ? result.Detail
            : $"{FulfilmentModes.Simulation(run.Mode)} reached {result.State}. {result.Detail}".Trim();
    }

    private IReadOnlyList<GapRecord> Resolved(FulfilmentRun run, IReadOnlyList<GapRecord> gaps)
    {
        var states = gaps.Any(gap => gap.Outcome is GapOutcome.Attempting or GapOutcome.Bidding or GapOutcome.Won)
            ? market_.Standing()
            : [];
        var result = gaps.Select(gap => StandingBids.Resolve(gap, states)).Select(gap => Banked(run, gap, states))
            .OrderBy(gap => gap.SlotIndex).ToList();

        foreach (var gap in result) store_.SaveGap(run.Id, gap);

        return result;
    }

    private GapRecord Banked(FulfilmentRun run, GapRecord gap, IReadOnlyList<TradeState> states)
    {
        var waiting = run.BuysLive && gap.Outcome == GapOutcome.Won && gap.TradeId is not null
            ? states.FirstOrDefault(state => state.TradeId == gap.TradeId)
            : null;

        return waiting is null ? gap : Claimed(gap, waiting);
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

        return new BuyingResult(halt.Length > 0 ? state : FulfilmentState.Buying, halt, settled);
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
        var shortlist = GapSnipePlan.Shortlist(gap.Specification, search.Listings, ceiling);
        GapStep result;

        Remember(shortlist);

        if (shortlist.Count == 0) result = new GapStep(Missed(searched, search, ceiling), string.Empty,
            FulfilmentState.Buying);
        else if (!ledger.Allows(gap.SlotIndex, MarketCandidateChoice.Price(shortlist[0])))
            result = new GapStep(searched with { Detail = CEILING_DETAIL }, CEILING_DETAIL,
                FulfilmentState.Aborted);
        else result = Take(run, searched, shortlist, ceiling, ledger);

        return result;
    }

    private GapStep Take(FulfilmentRun run, GapRecord gap, IReadOnlyList<AuctionListing> shortlist, uint ceiling,
        SpendLedger ledger)
    {
        var opening = MarketCandidateChoice.Price(shortlist[0]);

        ledger.Commit(gap.SlotIndex, opening);

        return run.BuysLive
            ? Snipe(run, gap, shortlist, ceiling)
            : new GapStep(Marked(gap, new MarketChoice(shortlist[0], opening)) with
            {
                Outcome = GapOutcome.Simulated, Simulated = true,
                Detail = $"would watch {shortlist.Count} card(s) and bid up to {ceiling}"
            }, string.Empty, FulfilmentState.Buying);
    }

    private GapStep Snipe(FulfilmentRun run, GapRecord gap, IReadOnlyList<AuctionListing> shortlist, uint ceiling)
    {
        var watched = market_.Watch(shortlist);
        var mine = shortlist.Select(listing => listing.TradeId).ToList();

        return watched == 0
            ? new GapStep(gap with { Outcome = GapOutcome.NotFound, Simulated = false, Detail = UNWATCHED_DETAIL },
                string.Empty, FulfilmentState.Buying)
            : Poll(run, Watching(gap, watched), ceiling, mine);
    }

    private GapStep Poll(FulfilmentRun run, GapRecord gap, uint ceiling, IReadOnlyList<string> mine)
    {
        var current = gap;
        var targets = GapSnipePlan.Watched(market_.Standing(), mine);
        var polls = 0;
        var finished = false;

        while (!finished && polls < MAX_POLLS)
        {
            current = Stepped(run, current, targets, ceiling);
            polls++;
            finished = Done(current, targets);

            if (!finished)
            {
                market_.Pause(POLL_MS);
                targets = GapSnipePlan.Watched(market_.Targets(), mine);
            }
        }

        return new GapStep(current, current.Outcome == GapOutcome.Unresolved ? Names(current) : string.Empty,
            FulfilmentState.Failed);
    }

    private GapRecord Stepped(FulfilmentRun run, GapRecord gap, IReadOnlyList<TradeState> targets, uint ceiling)
    {
        var next = Act(run, gap, targets, ceiling);

        if (next != gap) store_.SaveGap(run.Id, next);

        return next;
    }

    private GapRecord Act(FulfilmentRun run, GapRecord gap, IReadOnlyList<TradeState> targets, uint ceiling)
    {
        var won = GapSnipePlan.Won(targets);
        var due = won is null ? GapSnipePlan.Due(targets, ceiling) : null;
        GapRecord result;

        if (won is not null) result = Claimed(gap, won);
        else if (due is not null) result = Sniped(run, gap, due);
        else result = gap;

        return result;
    }

    private GapRecord Claimed(GapRecord gap, TradeState won)
    {
        var claimed = market_.Claim(won);

        return gap with
        {
            Outcome = GapOutcome.Won, TradeId = won.TradeId, ItemId = won.ItemId > 0 ? won.ItemId : gap.ItemId,
            AssetId = Bought(won.TradeId) ?? gap.AssetId, Simulated = false,
            FinalPrice = Paid(gap, won),
            Detail = claimed
                ? $"won at {Paid(gap, won)} and sent to the club"
                : $"won at {Paid(gap, won)} but it is still on the transfer targets"
        };
    }

    private int? Bought(string tradeId)
    {
        return seen_.TryGetValue(tradeId, out var listing) && listing.AssetId > 0 ? listing.AssetId : null;
    }

    private static int Paid(GapRecord gap, TradeState won)
    {
        return won.CurrentBid > 0 ? (int)won.CurrentBid : gap.BidAmount.GetValueOrDefault();
    }

    private GapRecord Sniped(FulfilmentRun run, GapRecord gap, SnipeTarget due)
    {
        var attempting = gap with
        {
            Outcome = GapOutcome.Attempting, Simulated = false, TradeId = due.Trade.TradeId,
            ItemId = due.Trade.ItemId, BidAmount = (int)due.Amount,
            Detail = $"bidding {due.Amount} with {due.Trade.SecondsLeft} s left"
        };

        store_.SaveGap(run.Id, attempting);

        var receipt = market_.BidOnTarget(due.Trade, due.Amount);

        return attempting with
        {
            Outcome = Landed(receipt), Detail = receipt.Detail,
            BidAmount = receipt.Placed ? (int)receipt.Amount : attempting.BidAmount
        };
    }

    private static GapRecord Watching(GapRecord gap, int watched)
    {
        return gap with
        {
            Outcome = GapOutcome.Bidding, Simulated = false, TradeId = null, ItemId = null, AssetId = null,
            BidAmount = null, FinalPrice = null,
            Detail = $"watching {watched} card(s) into their last seconds"
        };
    }

    private static bool Done(GapRecord gap, IReadOnlyList<TradeState> targets)
    {
        return gap.Outcome is GapOutcome.Won or GapOutcome.Unresolved or GapOutcome.Mismatched ||
               GapSnipePlan.Finished(targets);
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
        var fitting = search.Listings.Where(listing => MarketCandidateChoice.Matches(gap.Specification, listing))
            .ToList();
        var affordable = fitting.Any(listing => MarketCandidateChoice.Price(listing) <= ceiling);

        return gap with
        {
            Outcome = fitting.Count > 0 ? GapOutcome.TooExpensive : GapOutcome.NotFound,
            Detail = Reason(fitting.Count, affordable, ceiling)
        };
    }

    private static string Reason(int fitting, bool affordable, uint ceiling)
    {
        var result = $"no card matched under {ceiling}";

        if (fitting > 0 && affordable)
            result = $"{fitting} card(s) matched under {ceiling} but none ends within " +
                     $"{GapSnipePlan.WATCH_MAX_SECONDS} s";
        else if (fitting > 0) result = $"nothing matching sat at or under {ceiling}";

        return result;
    }

    private static string Names(GapRecord gap)
    {
        return $"slot {gap.SlotIndex}: {gap.Detail}";
    }

    private sealed record GapStep(GapRecord Gap, string Halt, FulfilmentState State);
}
