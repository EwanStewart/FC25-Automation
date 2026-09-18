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

        List<string> toRun = new()
        {
            ""
        };

        foreach (var run in toRun) _ = new Fc25(run, smokeTest);

        if (!smokeTest) Utility.Utility.ShutdownPc();
    }
}