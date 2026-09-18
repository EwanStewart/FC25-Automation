namespace Automation.Trading;

public static class SearchBudget
{
    public static int WaitSeconds(IEnumerable<DateTime> searches, DateTime now, int perMinute, int perHour)
    {
        var ordered = searches.Where(time => time <= now).OrderByDescending(time => time).ToList();
        var minuteWait = WindowWait(ordered, now, TimeSpan.FromMinutes(1), perMinute);
        var hourWait = WindowWait(ordered, now, TimeSpan.FromHours(1), perHour);

        return Math.Max(minuteWait, hourWait);
    }

    private static int WindowWait(IReadOnlyList<DateTime> newestFirst, DateTime now, TimeSpan window, int cap)
    {
        var inWindow = newestFirst.Where(time => now - time < window).ToList();
        var result = 0;

        if (inWindow.Count >= cap) result = (int)Math.Ceiling((inWindow[cap - 1] + window - now).TotalSeconds);

        return Math.Max(0, result);
    }
}
