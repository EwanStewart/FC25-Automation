using Automation.Trading;

namespace Automation.Tests;

public class CapacityTests
{
    [Fact]
    public void BiddingStopsWhenEitherPileIsFullOrTheBidLimitIsReached()
    {
        Assert.True(Capacity.CanBid(10, 49, 20, 100, 0, uint.MaxValue));
        Assert.False(Capacity.CanBid(49, 49, 20, 100, 0, uint.MaxValue));
        Assert.False(Capacity.CanBid(10, 49, 100, 100, 0, uint.MaxValue));
        Assert.False(Capacity.CanBid(10, 49, 20, 100, 1, 1));
    }

    [Fact]
    public void ScanContinuesOnlyInsideItsTimeBudget()
    {
        var started = new DateTime(2026, 9, 18, 20, 0, 0, DateTimeKind.Utc);

        Assert.True(Capacity.WithinBudget(started, started.AddSeconds(119), 120));
        Assert.False(Capacity.WithinBudget(started, started.AddSeconds(120), 120));
    }
}
