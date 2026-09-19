using Automation.Sbc;
using Automation.Sbc.Fulfilment;
using Automation.Trading;

namespace Automation.Tests;

public sealed class RecordingStore : IFulfilmentStore
{
    private readonly Dictionary<int, GapRecord> gaps_ = new();
    private readonly Dictionary<int, PlacementRecord> placements_ = new();

    public RecordingStore(List<string>? log = null)
    {
        Log = log ?? [];
    }

    public List<string> Log { get; }

    public List<FulfilmentRun> Runs { get; } = [];

    public FulfilmentState State { get; private set; } = FulfilmentState.Pending;

    public string Detail { get; private set; } = "";

    public IReadOnlyList<FulfilmentRun> Queued()
    {
        return Runs;
    }

    public IReadOnlyList<GapRecord> Gaps(int fulfilmentId)
    {
        return gaps_.Values.OrderBy(gap => gap.SlotIndex).ToList();
    }

    public IReadOnlyList<PlacementRecord> Placements(int fulfilmentId)
    {
        return placements_.Values.OrderBy(placement => placement.SlotIndex).ToList();
    }

    public void SaveGap(int fulfilmentId, GapRecord gap)
    {
        gaps_[gap.SlotIndex] = gap;
        Log.Add($"gap {gap.SlotIndex} {gap.Outcome} {gap.BidAmount?.ToString() ?? "-"}");
    }

    public void SavePlacement(int fulfilmentId, PlacementRecord placement)
    {
        placements_[placement.SlotIndex] = placement;
        Log.Add($"place {placement.SlotIndex} {placement.Outcome}");
    }

    public void SaveState(int fulfilmentId, FulfilmentState state, string detail)
    {
        State = state;
        Detail = detail;
        Log.Add($"state {state}");
    }
}

public sealed class ScriptedMarket : IMarketAgent
{
    private readonly Dictionary<int, IReadOnlyList<AuctionListing>> results_;
    private readonly Dictionary<string, BidOutcome> outcomes_;
    private readonly List<string> log_;

    public bool Withhold { get; init; }

    public uint? Actual { get; init; }

    public ScriptedMarket(List<string> log, Dictionary<int, IReadOnlyList<AuctionListing>> results,
        Dictionary<string, BidOutcome>? outcomes = null, IReadOnlyList<TradeState>? standing = null)
    {
        log_ = log;
        results_ = results;
        outcomes_ = outcomes ?? [];
        Standings = standing ?? [];
    }

    public IReadOnlyList<TradeState> Standings { get; }

    public List<uint> Ceilings { get; } = [];

    public MarketSearch Search(MarketSpecification specification, uint ceiling)
    {
        Ceilings.Add(ceiling);
        log_.Add($"search {specification.Position} under {ceiling}");

        return new MarketSearch($"{specification.Quality} {specification.Position} under {ceiling}",
            results_.GetValueOrDefault(Ceilings.Count - 1, []));
    }

    public BidReceipt Bid(MarketChoice choice, uint amount)
    {
        log_.Add($"bid {choice.Listing.TradeId} {amount}");

        return new BidReceipt(outcomes_.GetValueOrDefault(choice.Listing.TradeId, BidOutcome.Registered),
            Actual ?? amount, "scripted", !Withhold);
    }

    public IReadOnlyList<TradeState> Standing()
    {
        return Standings;
    }
}

public static class FulfilmentFixtures
{
    public static MarketSpecification Specification(int estimate = 900)
    {
        return new MarketSpecification("CB", PlayerQuality.Silver, 65, 74, 14, 13, null, null, estimate);
    }

    public static AuctionListing Listing(string tradeId, uint startingBid, int rating = 68)
    {
        return new AuctionListing(tradeId, "active", "none", 300, 0, startingBid, 0, rating, "CB", 0, 0, 1, 1, 0,
            13, 14, 0, []);
    }

    public static GapRecord Gap(int slot, int estimate = 900, GapOutcome outcome = GapOutcome.Pending)
    {
        return new GapRecord(slot, "CB", Specification(estimate), CardCeiling.For(estimate), outcome);
    }

    public static FulfilmentRun Run(FulfilmentMode mode, long ceiling,
        FulfilmentState state = FulfilmentState.Pending)
    {
        return new FulfilmentRun(1, 7, 1234, state, mode, ceiling, 2000, "");
    }
}

public sealed class ScriptedSquad : ISquadAgent
{
    private readonly List<string> log_;
    private readonly Dictionary<int, SquadSlotView> slots_;

    public ScriptedSquad(List<string> log, IReadOnlyList<SquadSlotView> slots)
    {
        log_ = log;
        slots_ = slots.ToDictionary(slot => slot.Index);
    }

    public int? Refuse { get; init; }

    public SquadView Open(int challengeId)
    {
        log_.Add($"open {challengeId}");

        return View(challengeId);
    }

    public void Place(int slotIndex, SquadTarget target)
    {
        log_.Add($"place-in-app {slotIndex} {target.ItemId}");

        if (Refuse != slotIndex && slots_.TryGetValue(slotIndex, out var slot))
            slots_[slotIndex] = slot with { ItemId = target.ItemId, Filled = true };
    }

    public SquadView Read(int challengeId)
    {
        log_.Add($"read {challengeId}");

        return View(challengeId);
    }

    private SquadView View(int challengeId)
    {
        return new SquadView(challengeId, "442", slots_.Values.OrderBy(slot => slot.Index).ToList());
    }
}

public sealed class RefusingMarket : IMarketAgent
{
    public int Searches { get; private set; }

    public int Bids { get; private set; }

    public MarketSearch Search(MarketSpecification specification, uint ceiling)
    {
        Searches++;

        return new MarketSearch("refusing", [FulfilmentFixtures.Listing("t1", 100)]);
    }

    public BidReceipt Bid(MarketChoice choice, uint amount)
    {
        Bids++;

        throw new InvalidOperationException("A bid was sent when no bid should have been reachable.");
    }

    public IReadOnlyList<TradeState> Standing()
    {
        return [];
    }
}

public sealed class RefusingSquad : ISquadAgent
{
    public SquadView Open(int challengeId)
    {
        throw new InvalidOperationException($"The squad for challenge {challengeId} never came back from the app.");
    }

    public void Place(int slotIndex, SquadTarget target)
    {
        throw new InvalidOperationException($"Slot {slotIndex} cannot take {target.Name}.");
    }

    public SquadView Read(int challengeId)
    {
        throw new InvalidOperationException($"The squad for challenge {challengeId} cannot be read.");
    }
}
