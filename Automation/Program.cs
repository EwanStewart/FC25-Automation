namespace Automation;

internal class Program
{
    private const long SBC_DEFAULT_BUDGET = 50000;

    /// <summary>
    /// Main entry point to application.
    /// </summary>
    /// <param name="args">CLI arguments.</param>
    private static void Main(string[] args)
    {
        var options = Trading.RunOptions.Parse(args);

        if (options.ImportPlayers) Environment.Exit(Catalogue.CatalogueProgram.Run());
        else if (options.CaptureClub) CaptureClub(options);
        else if (options.DraftSbc) Sbc.SbcProgram.Report(SBC_DEFAULT_BUDGET);
        else RunTradingConfigurations(options);
    }

    private static void RunTradingConfigurations(Trading.RunOptions options)
    {
        List<string> toRun = new()
        {
            ""
        };

        foreach (var run in toRun) RunConfiguration(run, options);

        if (!options.SmokeTest && !options.NoShutdown && options.LoopMinutes == 0) Utility.Utility.ShutdownPc();
    }

    private static void CaptureClub(Trading.RunOptions options)
    {
        using var bot = new Fc25("", options);
        var reading = bot.CaptureClubInventory();

        Console.WriteLine($"Club capture {reading.Outcome}: {reading.Players.Count} players. {reading.Detail}");
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