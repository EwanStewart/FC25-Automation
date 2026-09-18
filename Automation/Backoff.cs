namespace Automation.Trading;

public enum BackoffAction
{
    Continue,
    SlowDown,
    EndPass,
    StopRun
}

public sealed class RunStoppedException : Exception
{
    public RunStoppedException(string message) : base(message)
    {
    }
}

public static class Backoff
{
    public static readonly IReadOnlySet<int> THROTTLE_STATUSES = new HashSet<int> { 429, 512 };
    public static readonly IReadOnlySet<int> FATAL_STATUSES = new HashSet<int> { 458, 426, 494 };

    public static BackoffAction Decide(IEnumerable<int> statuses)
    {
        var seen = statuses.ToList();
        var throttles = seen.Count(THROTTLE_STATUSES.Contains);
        var result = BackoffAction.Continue;

        if (seen.Any(FATAL_STATUSES.Contains)) result = BackoffAction.StopRun;
        else if (throttles >= 2) result = BackoffAction.EndPass;
        else if (throttles == 1) result = BackoffAction.SlowDown;

        return result;
    }
}
