namespace Automation;

internal class Program
{
    /// <summary>
    /// Main entry point to application.
    /// </summary>
    /// <param name="args">CLI arguments.</param>
    private static void Main(string[] args)
    {
        var smokeTest = args.Contains("--smoke");
        var loopMinutes = LoopMinutes(args);

        List<string> toRun = new()
        {
            ""
        };

        foreach (var run in toRun) RunConfiguration(run, smokeTest, loopMinutes);

        if (!smokeTest && loopMinutes == 0) Utility.Utility.ShutdownPc();
    }

    private static uint LoopMinutes(string[] args)
    {
        var index = Array.IndexOf(args, "--loop");
        uint result = 0;

        if (index >= 0)
            result = index + 1 < args.Length && uint.TryParse(args[index + 1], out var minutes)
                ? minutes
                : Trading.BiddingStrategy.LOOP_MINUTES_DEFAULT;

        return result;
    }

    private static void RunConfiguration(string run, bool smokeTest, uint loopMinutes)
    {
        var bot = new Fc25(run, smokeTest);

        while (loopMinutes > 0)
        {
            Console.WriteLine($"Next cycle in {loopMinutes} minutes.");
            Thread.Sleep(TimeSpan.FromMinutes(loopMinutes));
            bot.RunCycleSafely();
        }
    }
}