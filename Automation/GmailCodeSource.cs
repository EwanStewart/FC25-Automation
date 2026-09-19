using System.Diagnostics;

namespace Automation.Flow;

public interface IVerificationCodeSource
{
    string WaitForCode(DateTimeOffset requestedAt, TimeSpan budget);
}

public sealed class GmailCodeSource : IVerificationCodeSource
{
    private const string SCRIPT_NAME = "fetch_ea_code.py";
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    public string WaitForCode(DateTimeOffset requestedAt, TimeSpan budget)
    {
        var result = string.Empty;
        var cutoff = requestedAt - Grace;
        var deadline = DateTime.UtcNow + budget;

        while (result.Length == 0 && DateTime.UtcNow < deadline)
        {
            result = VerificationCode.FromFeed(ReadFeed(cutoff), cutoff);

            if (result.Length == 0) Thread.Sleep(PollInterval);
        }

        return result;
    }

    private static IEnumerable<string> ReadFeed(DateTimeOffset cutoff)
    {
        var result = Array.Empty<string>();
        var script = FindScript();

        if (script != null) result = RunScript(script, cutoff);

        return result;
    }

    private static string[] RunScript(string script, DateTimeOffset cutoff)
    {
        var result = Array.Empty<string>();
        var startInfo = new ProcessStartInfo("python3")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add(script);
        startInfo.ArgumentList.Add("--after");
        startInfo.ArgumentList.Add(cutoff.ToUnixTimeSeconds().ToString());

        try
        {
            using var process = Process.Start(startInfo);

            if (process != null)
            {
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit((int)TimeSpan.FromSeconds(60).TotalMilliseconds);
                result = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Verification code lookup failed: {exception.Message}");
        }

        return result;
    }

    private static string? FindScript()
    {
        string? result = null;
        var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);

        while (directory != null && result == null)
        {
            var candidate = Path.Combine(directory.FullName, "scripts", SCRIPT_NAME);
            if (File.Exists(candidate)) result = candidate;
            directory = directory.Parent;
        }

        return result;
    }
}
