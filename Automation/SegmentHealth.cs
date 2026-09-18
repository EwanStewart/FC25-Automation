namespace Automation.Trading;

public readonly record struct SegmentRecord(string Segment, int Won, int Lost, long Profit, DateTime? LastBidAt);

public enum SegmentVerdict
{
    Active,
    Cooling,
    Probing
}

public static class SegmentHealth
{
    public static double? WinRate(int won, int lost, int minResolved)
    {
        var resolved = won + lost;
        double? result = null;

        if (resolved >= minResolved && resolved > 0) result = (double)won / resolved;

        return result;
    }

    public static bool IsDemoted(int won, int lost, int minResolved, double minWinRate)
    {
        var winRate = WinRate(won, lost, minResolved);

        return winRate.HasValue && winRate.Value < minWinRate;
    }

    public static SegmentVerdict Judge(SegmentRecord record, DateTime now, int minResolved, double minWinRate,
        uint cooldownHours)
    {
        var result = SegmentVerdict.Active;

        if (IsDemoted(record.Won, record.Lost, minResolved, minWinRate))
            result = HasRested(record.LastBidAt, now, cooldownHours) ? SegmentVerdict.Probing : SegmentVerdict.Cooling;

        return result;
    }

    public static uint BidAllowance(SegmentVerdict verdict, uint probeBids)
    {
        var result = uint.MaxValue;

        if (verdict == SegmentVerdict.Cooling) result = 0;
        else if (verdict == SegmentVerdict.Probing) result = probeBids;

        return result;
    }

    private static bool HasRested(DateTime? lastBidAt, DateTime now, uint cooldownHours)
    {
        return !lastBidAt.HasValue || now - lastBidAt.Value >= TimeSpan.FromHours(cooldownHours);
    }
}
