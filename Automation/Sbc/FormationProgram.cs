using Automation.Setup;

namespace Automation.Sbc;

public static class FormationProgram
{
    private const string SOLUTION_FILE = "Automation.sln";
    private const string DATA_FILE = "formations.json";
    private const string PROJECT_FOLDER = "Automation";
    private const string SBC_FOLDER = "Sbc";
    private const int SUCCESS = 0;
    private const int FAILURE = 1;

    public static int Run()
    {
        return Run(DefaultOutputPath());
    }

    public static int Run(string outputPath)
    {
        var reading = CaptureThroughTheBrowser(outputPath);

        Console.WriteLine($"Formation capture wrote {reading.Layouts.Count} layouts to {outputPath}.");

        return reading.Outcome == FormationCaptureOutcome.Captured ? SUCCESS : FAILURE;
    }

    public static string DefaultOutputPath()
    {
        return Path.Combine(SolutionDirectory(), PROJECT_FOLDER, SBC_FOLDER, DATA_FILE);
    }

    private static FormationCaptureReading CaptureThroughTheBrowser(string outputPath)
    {
        Browser browser = new(string.Empty);
        var driver = browser.Chrome;
        using MouseInput mouse = new();

        mouse.Start(Browser.DebuggerHttp);

        try
        {
            return new FormationCapture(driver, new Screen(driver), mouse, outputPath).Capture();
        }
        finally
        {
            driver.Quit();
        }
    }

    private static string SolutionDirectory()
    {
        string? result = null;
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null && result == null)
        {
            if (File.Exists(Path.Combine(directory.FullName, SOLUTION_FILE))) result = directory.FullName;
            directory = directory.Parent;
        }

        if (result == null)
            throw new FormationLayoutException($"No directory above {AppContext.BaseDirectory} holds {SOLUTION_FILE}.");

        return result;
    }
}
