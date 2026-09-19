namespace Automation.Tests;

public static class SbcFixture
{
    private const string FIXTURE_FOLDER = "SbcFixtures";
    private const string PROJECT_FILE = "Automation.Tests.csproj";

    public static string Read(string fileName)
    {
        return File.ReadAllText(Path.Combine(ProjectDirectory(), FIXTURE_FOLDER, fileName));
    }

    private static string ProjectDirectory()
    {
        string? result = null;
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null && result == null)
        {
            if (File.Exists(Path.Combine(directory.FullName, PROJECT_FILE))) result = directory.FullName;
            directory = directory.Parent;
        }

        if (result == null)
            throw new DirectoryNotFoundException($"No directory above {AppContext.BaseDirectory} holds {PROJECT_FILE}.");

        return result;
    }
}
