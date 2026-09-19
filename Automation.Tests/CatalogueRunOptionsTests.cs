using Automation.Trading;

namespace Automation.Tests;

public class CatalogueRunOptionsTests
{
    [Fact]
    public void TheImportIsOffUnlessItIsAskedFor()
    {
        Assert.False(RunOptions.Parse([]).ImportPlayers);
        Assert.False(RunOptions.Parse(["--snipe"]).ImportPlayers);
    }

    [Fact]
    public void ImportPlayersTurnsTheCatalogueImportOn()
    {
        Assert.True(RunOptions.Parse(["--import-players"]).ImportPlayers);
        Assert.True(RunOptions.Parse(["--no-shutdown", "--import-players"]).ImportPlayers);
    }
}
