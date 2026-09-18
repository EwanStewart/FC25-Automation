using Automation.Trading;

namespace Automation.Tests;

public class PassPacingTests
{
    private static readonly DateTime Now = new(2026, 9, 18, 21, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void FirstPageTurnAndCompareReadNeedNoWait()
    {
        var pacing = new PassPacing(2500, 3000);

        Assert.Equal(0, pacing.PageTurnWaitMs(Now, 700));
        Assert.Equal(0, pacing.CompareWaitMs(Now));
    }

    [Fact]
    public void PageTurnsKeepTheGapPlusJitter()
    {
        var pacing = new PassPacing(2500, 3000);

        pacing.RecordPageTurn(Now);

        Assert.Equal(2200, pacing.PageTurnWaitMs(Now.AddSeconds(1), 700));
        Assert.Equal(0, pacing.PageTurnWaitMs(Now.AddSeconds(3.2), 700));
        Assert.Equal(1, pacing.PagesTurned);
    }

    [Fact]
    public void CompareReadsKeepTheirOwnGap()
    {
        var pacing = new PassPacing(2500, 3000);

        pacing.RecordCompareRead(Now);

        Assert.Equal(1000, pacing.CompareWaitMs(Now.AddSeconds(2)));
        Assert.Equal(0, pacing.CompareWaitMs(Now.AddSeconds(3)));
    }

    [Fact]
    public void SlowingDownDoublesBothGapsAndCountsTheBackoff()
    {
        var pacing = new PassPacing(2500, 3000);

        pacing.RecordPageTurn(Now);
        pacing.RecordCompareRead(Now);
        pacing.SlowDown();

        Assert.Equal(2, pacing.Multiplier);
        Assert.Equal(5500, pacing.PageTurnWaitMs(Now, 500));
        Assert.Equal(6000, pacing.CompareWaitMs(Now));
        Assert.Equal(1, pacing.Backoffs);
    }

    [Fact]
    public void WaitsAreTalliedAndEndingIsSticky()
    {
        var pacing = new PassPacing(2500, 3000);

        pacing.RecordWait(1200);
        pacing.RecordWait(800);
        pacing.End();

        Assert.Equal(2, pacing.Waits);
        Assert.Equal(2000, pacing.WaitMs);
        Assert.True(pacing.Ended);
    }
}
