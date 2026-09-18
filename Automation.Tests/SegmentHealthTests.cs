using Automation.Trading;

namespace Automation.Tests;

public class SegmentHealthTests
{
    private static readonly DateTime Now = new(2026, 9, 18, 17, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void WinRateNeedsEnoughResolvedBids()
    {
        Assert.Null(SegmentHealth.WinRate(2, 7, 10));
        Assert.Equal(0.2, SegmentHealth.WinRate(2, 8, 10));
    }

    [Theory]
    [InlineData(1, 9, false)]
    [InlineData(0, 10, true)]
    [InlineData(1, 19, true)]
    [InlineData(0, 9, false)]
    public void IsDemotedWhenTheWinRateFallsBelowTheThreshold(int won, int lost, bool expected)
    {
        Assert.Equal(expected, SegmentHealth.IsDemoted(won, lost, 10, 0.1));
    }

    [Fact]
    public void JudgeKeepsHealthySegmentsActive()
    {
        SegmentRecord record = new("kits", 5, 5, 0, Now.AddMinutes(-10));

        Assert.Equal(SegmentVerdict.Active, SegmentHealth.Judge(record, Now, 10, 0.1, 6));
    }

    [Fact]
    public void JudgeCoolsADemotedSegmentUntilTheCooldownPasses()
    {
        SegmentRecord recent = new("kits", 0, 12, 0, Now.AddHours(-2));
        SegmentRecord rested = new("kits", 0, 12, 0, Now.AddHours(-7));
        SegmentRecord never = new("kits", 0, 12, 0, null);

        Assert.Equal(SegmentVerdict.Cooling, SegmentHealth.Judge(recent, Now, 10, 0.1, 6));
        Assert.Equal(SegmentVerdict.Probing, SegmentHealth.Judge(rested, Now, 10, 0.1, 6));
        Assert.Equal(SegmentVerdict.Probing, SegmentHealth.Judge(never, Now, 10, 0.1, 6));
    }

    [Fact]
    public void BidAllowanceFollowsTheVerdict()
    {
        Assert.Equal(uint.MaxValue, SegmentHealth.BidAllowance(SegmentVerdict.Active, 3));
        Assert.Equal(0u, SegmentHealth.BidAllowance(SegmentVerdict.Cooling, 3));
        Assert.Equal(3u, SegmentHealth.BidAllowance(SegmentVerdict.Probing, 3));
    }
}
