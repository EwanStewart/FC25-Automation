namespace Automation;

internal class Program
{
    /// <summary>
    /// Main entry point to application.
    /// </summary>
    /// <param name="args">CLI arguments.</param>
    private static void Main(string[] args)
    {
        var options = Trading.RunOptions.Parse(args);

        List<string> toRun = new()
        {
            ""
        };

        foreach (var run in toRun) RunConfiguration(run, options);

        if (!options.SmokeTest && !options.NoShutdown && options.LoopMinutes == 0) Utility.Utility.ShutdownPc();
    }

    private static void RunConfiguration(string run, Trading.RunOptions options)
    {
        using var bot = new Fc25(run, options);

        while (options.LoopMinutes > 0)
        {
            Console.WriteLine($"Next cycle in {options.LoopMinutes} minutes.");
            Thread.Sleep(TimeSpan.FromMinutes(options.LoopMinutes));
            bot.RunCycleSafely();
        }
    }
}