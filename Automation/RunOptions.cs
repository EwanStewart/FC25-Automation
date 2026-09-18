namespace Automation.Trading;

public sealed record RunOptions(bool SmokeTest, string SmokeTarget, bool NoShutdown, uint LoopMinutes, bool SnipeOnly)
{
    private const string SMOKE = "--smoke";
    private const string LOOP = "--loop";

    public static RunOptions Parse(IReadOnlyList<string> args)
    {
        return new RunOptions(args.Contains(SMOKE), ValueAfter(args, SMOKE) ?? "all", args.Contains("--no-shutdown"),
            ParseLoopMinutes(args), args.Contains("--snipe"));
    }

    private static uint ParseLoopMinutes(IReadOnlyList<string> args)
    {
        var result = 0u;

        if (args.Contains(LOOP))
            result = uint.TryParse(ValueAfter(args, LOOP), out var minutes) ? minutes : BiddingStrategy.LOOP_MINUTES_DEFAULT;

        return result;
    }

    private static string? ValueAfter(IReadOnlyList<string> args, string flag)
    {
        var index = args.ToList().IndexOf(flag);
        var next = index >= 0 && index + 1 < args.Count ? args[index + 1] : null;

        return next != null && !next.StartsWith("--", StringComparison.Ordinal) ? next : null;
    }
}
