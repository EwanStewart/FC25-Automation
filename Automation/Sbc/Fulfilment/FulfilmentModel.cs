using Automation.Trading;

namespace Automation.Sbc.Fulfilment;

public enum FulfilmentState
{
    Pending,
    Placing,
    Buying,
    Built,
    Failed,
    Aborted
}

public enum GapOutcome
{
    Pending,
    Attempting,
    Bidding,
    Won,
    Outbid,
    Expired,
    NotFound,
    TooExpensive,
    Unmapped,
    Simulated,
    Unresolved
}

public enum PlacementOutcome
{
    Pending,
    Placed,
    Missing,
    Refused,
    Simulated
}

public sealed record FulfilmentRun(
    int Id,
    int ApprovalId,
    int ChallengeId,
    FulfilmentState State,
    FulfilmentMode Mode,
    long SpendCeiling,
    long EstimatedCost,
    string Detail)
{
    public bool DryRun => Mode == FulfilmentMode.Dry;

    public bool BuysLive => Mode == FulfilmentMode.Live;

    public bool PlacesLive => Mode is FulfilmentMode.PlaceLive or FulfilmentMode.Live;
}

public sealed record GapRecord(
    int SlotIndex,
    string Position,
    MarketSpecification Specification,
    int CardCeiling,
    GapOutcome Outcome = GapOutcome.Pending,
    bool Simulated = true,
    string Searched = "",
    int CandidatesSeen = 0,
    string? TradeId = null,
    long? ItemId = null,
    int? AssetId = null,
    int? BidAmount = null,
    int? FinalPrice = null,
    string Detail = "");

public sealed record PlacementRecord(
    int SlotIndex,
    string Position,
    string Source,
    long? ClubPlayerId,
    string PlayerName,
    PlacementOutcome Outcome = PlacementOutcome.Pending,
    bool Simulated = true,
    string Detail = "");

public sealed record MarketSearch(string Description, IReadOnlyList<AuctionListing> Listings);

public sealed record BidReceipt(BidOutcome Outcome, uint Amount, string Detail, bool Placed = true);

public sealed record TradeState(
    string TradeId,
    string State,
    string BidState,
    uint CurrentBid,
    int? SecondsLeft = null,
    uint MinimumBid = 0,
    long ItemId = 0);

public interface IMarketAgent
{
    MarketSearch Search(MarketSpecification specification, uint ceiling);

    BidReceipt Bid(MarketChoice choice, uint amount);

    IReadOnlyList<TradeState> Standing();
}

public interface IFulfilmentStore
{
    IReadOnlyList<FulfilmentRun> Queued();

    IReadOnlyList<GapRecord> Gaps(int fulfilmentId);

    IReadOnlyList<PlacementRecord> Placements(int fulfilmentId);

    void SaveGap(int fulfilmentId, GapRecord gap);

    void SavePlacement(int fulfilmentId, PlacementRecord placement);

    void SaveState(int fulfilmentId, FulfilmentState state, string detail);
}

public static class GapProgress
{
    public static bool Retryable(GapOutcome outcome)
    {
        return outcome is GapOutcome.Pending or GapOutcome.Outbid or GapOutcome.Expired or GapOutcome.NotFound
            or GapOutcome.TooExpensive or GapOutcome.Unmapped or GapOutcome.Simulated;
    }

    public static bool Holds(GapOutcome outcome)
    {
        return outcome is GapOutcome.Attempting or GapOutcome.Bidding or GapOutcome.Won;
    }

    public static bool Settled(GapOutcome outcome)
    {
        return outcome is GapOutcome.Won or GapOutcome.Simulated;
    }
}
