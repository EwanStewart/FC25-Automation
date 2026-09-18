using Automation.Trading;

namespace Automation.Tests;

public class RowTriageTests
{
    private const string PLAIN = "listFUTItem has-auction-data";

    private static bool IsCandidate(RowFacts facts)
    {
        return RowTriage.IsCandidate(facts, 3, 20, 1000);
    }

    [Fact]
    public void RowInsideTheWindowWithoutACachedPriceIsACandidate()
    {
        Assert.True(IsCandidate(new RowFacts(PLAIN, 5, false, 300, null, false)));
    }

    [Fact]
    public void RowsWeHoldOrAlreadyBidOnAreNotCandidates()
    {
        Assert.False(IsCandidate(new RowFacts($"{PLAIN} highest-bid", 5, false, 300, null, false)));
        Assert.False(IsCandidate(new RowFacts(PLAIN, 5, true, 300, null, false)));
    }

    [Theory]
    [InlineData(2u)]
    [InlineData(21u)]
    public void RowsOutsideTheWindowAreNotCandidates(uint minutes)
    {
        Assert.False(IsCandidate(new RowFacts(PLAIN, minutes, false, 300, null, false)));
        Assert.False(IsCandidate(new RowFacts(PLAIN, null, false, 300, null, false)));
    }

    [Fact]
    public void WithNoLowerBoundRowsInTheirLastMinuteAreCandidatesButEndedRowsAreNot()
    {
        Assert.True(RowTriage.IsCandidate(new RowFacts(PLAIN, 0, false, 300, null, false), 0, 20, 1000));
        Assert.True(RowTriage.IsCandidate(new RowFacts($"{PLAIN} outbid", 0, false, 300, null, false), 0, 20, 1000));
        Assert.False(RowTriage.IsCandidate(new RowFacts($"{PLAIN} expired", 0, false, 300, null, false), 0, 20, 1000));
        Assert.False(RowTriage.IsCandidate(new RowFacts($"{PLAIN} won", 0, false, 300, null, false), 0, 20, 1000));
    }

    [Fact]
    public void ACheapCachedPriceStopsRulingTheRowOutOnceItHasAgedOut()
    {
        Assert.False(IsCandidate(new RowFacts(PLAIN, 5, false, 300, 1300, false)));
        Assert.True(IsCandidate(new RowFacts(PLAIN, 5, false, 300, 1300, false, true)));
    }

    [Fact]
    public void CachedPriceBelowTheRequiredResaleRulesTheRowOutUnlessItHasSales()
    {
        Assert.False(IsCandidate(new RowFacts(PLAIN, 5, false, 300, 1300, false)));
        Assert.True(IsCandidate(new RowFacts(PLAIN, 5, false, 300, 1400, false)));
        Assert.True(IsCandidate(new RowFacts(PLAIN, 5, false, 300, 1300, true)));
        Assert.False(IsCandidate(new RowFacts(PLAIN, 5, false, null, 1000, false)));
    }
}
