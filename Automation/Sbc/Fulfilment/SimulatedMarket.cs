using Automation.Trading;

namespace Automation.Sbc.Fulfilment;

public sealed class SimulatedMarket : IMarketAgent
{
    private readonly IMarketAgent market_;

    public SimulatedMarket(IMarketAgent market)
    {
        market_ = market;
    }

    public MarketSearch Search(MarketSpecification specification, uint ceiling)
    {
        return market_.Search(specification, ceiling);
    }

    public BidReceipt Bid(MarketChoice choice, uint amount)
    {
        throw new InvalidOperationException(
            $"A run that is not buying live reached the bid path for trade {choice.Listing.TradeId} at {amount} coins.");
    }

    public IReadOnlyList<TradeState> Standing()
    {
        return market_.Standing();
    }

    public IReadOnlyList<TradeState> Targets()
    {
        return market_.Targets();
    }

    public int Watch(IReadOnlyList<AuctionListing> listings)
    {
        return 0;
    }

    public BidReceipt BidOnTarget(TradeState target, uint amount)
    {
        throw new InvalidOperationException(
            $"A run that is not buying live reached the bid path for trade {target.TradeId} at {amount} coins.");
    }

    public bool Claim(TradeState target)
    {
        return false;
    }

    public void Pause(int milliseconds)
    {
        market_.Pause(milliseconds);
    }
}
