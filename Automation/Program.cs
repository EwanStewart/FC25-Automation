namespace Automation;

internal class Program
{
    /// <summary>
    /// Main entry point to application.
    /// </summary>
    /// <param name="args">CLI arguments.</param>
    private static void Main(string[] args)
    {
        List<string> toRun = new()
        {
            ""
        };

        foreach (var run in toRun) _ = new Fc25(run);

        Utility.Utility.ShutdownPc();
    }
}