namespace Automation.Trading;

public sealed class PassPacing
{
    private readonly int _pageTurnGapMs;
    private readonly int _compareGapMs;
    private DateTime _lastPageTurn = DateTime.MinValue;
    private DateTime _lastCompareRead = DateTime.MinValue;

    public PassPacing(int pageTurnGapMs, int compareGapMs)
    {
        _pageTurnGapMs = pageTurnGapMs;
        _compareGapMs = compareGapMs;
    }

    public int Multiplier { get; private set; } = 1;

    public int PagesTurned { get; private set; }

    public int Waits { get; private set; }

    public int WaitMs { get; private set; }

    public int Backoffs { get; private set; }

    public bool Ended { get; private set; }

    public int PageTurnWaitMs(DateTime now, int jitterMs)
    {
        return Remaining(_lastPageTurn, now, _pageTurnGapMs * Multiplier + jitterMs);
    }

    public int CompareWaitMs(DateTime now)
    {
        return Remaining(_lastCompareRead, now, _compareGapMs * Multiplier);
    }

    public void RecordPageTurn(DateTime now)
    {
        _lastPageTurn = now;
        PagesTurned++;
    }

    public void RecordCompareRead(DateTime now)
    {
        _lastCompareRead = now;
    }

    public void RecordWait(int milliseconds)
    {
        Waits++;
        WaitMs += milliseconds;
    }

    public void SlowDown()
    {
        Multiplier = 2;
        Backoffs++;
    }

    public void End()
    {
        Ended = true;
    }

    private static int Remaining(DateTime last, DateTime now, int gapMs)
    {
        var elapsed = last == DateTime.MinValue ? double.MaxValue : (now - last).TotalMilliseconds;

        return elapsed >= gapMs ? 0 : (int)Math.Ceiling(gapMs - elapsed);
    }
}
