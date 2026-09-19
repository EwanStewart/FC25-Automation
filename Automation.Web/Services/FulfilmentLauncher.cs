using System.Diagnostics;

namespace Automation.Web.Services;

public sealed class FulfilmentLauncher
{
    private const string SCRIPT_NAME = "fulfil.sh";
    private const string LIVE_FLAG = "--fulfil-live";

    private readonly string scriptPath_;

    public FulfilmentLauncher(IHostEnvironment environment)
    {
        scriptPath_ = Locate(environment.ContentRootPath);
    }

    public string Start(bool live)
    {
        var result = $"{SCRIPT_NAME} was not found beside the web project.";

        if (scriptPath_.Length > 0)
        {
            var startInfo = new ProcessStartInfo(scriptPath_)
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(scriptPath_) ?? string.Empty
            };

            if (live) startInfo.ArgumentList.Add(LIVE_FLAG);

            Process.Start(startInfo);
            result = live
                ? "Live fulfilment started. It bids real coins; watch the newest fulfil log."
                : "Dry run started. Nothing will be bought; watch the newest fulfil log.";
        }

        return result;
    }

    private static string Locate(string contentRoot)
    {
        var result = string.Empty;
        var directory = new DirectoryInfo(contentRoot);

        while (directory != null && result.Length == 0)
        {
            var candidate = Path.Combine(directory.FullName, SCRIPT_NAME);

            if (File.Exists(candidate)) result = candidate;

            directory = directory.Parent;
        }

        return result;
    }
}
