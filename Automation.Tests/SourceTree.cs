namespace Automation.Tests;

public static class SourceTree
{
    public const string GUARD_FILE = "ForbiddenControls.cs";

    private const string MARKER = "Automation.sln";

    public static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null && !File.Exists(Path.Combine(directory.FullName, MARKER)))
            directory = directory.Parent;

        if (directory is null)
            throw new InvalidOperationException($"No {MARKER} was found above {AppContext.BaseDirectory}.");

        return directory.FullName;
    }
}
