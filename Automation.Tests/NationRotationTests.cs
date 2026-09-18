using Automation.Trading;

namespace Automation.Tests;

public class NationRotationTests
{
    private static readonly string[] Ring = { "England", "Germany", "France" };
    private static readonly HashSet<string> NoneCooling = new();

    [Fact]
    public void NextExploratoryWalksTheRingFromTheLastNation()
    {
        Assert.Equal("England", NationRotation.NextExploratory(Ring, null, NoneCooling));
        Assert.Equal("Germany", NationRotation.NextExploratory(Ring, "England", NoneCooling));
        Assert.Equal("England", NationRotation.NextExploratory(Ring, "France", NoneCooling));
        Assert.Equal("England", NationRotation.NextExploratory(Ring, "Atlantis", NoneCooling));
    }

    [Fact]
    public void NextExploratorySkipsCoolingNations()
    {
        Assert.Equal("France", NationRotation.NextExploratory(Ring, "England", new HashSet<string> { "Germany" }));
        Assert.Null(NationRotation.NextExploratory(Ring, "England", Ring.ToHashSet()));
    }

    [Fact]
    public void BestPerformerRanksPlayerNationsByProfitThenWinRate()
    {
        var records = new[]
        {
            new SegmentRecord("players:England", 2, 3, 500, null),
            new SegmentRecord("players:Germany", 1, 9, 900, null),
            new SegmentRecord("players:France", 4, 1, 500, null),
            new SegmentRecord("kits", 5, 0, 5000, null)
        };

        Assert.Equal("Germany", NationRotation.BestPerformer(records, NoneCooling));
        Assert.Equal("France", NationRotation.BestPerformer(records, new HashSet<string> { "Germany" }));
    }

    [Fact]
    public void BestPerformerNeedsAtLeastOneWin()
    {
        var records = new[] { new SegmentRecord("players:England", 0, 6, 0, null) };

        Assert.Null(NationRotation.BestPerformer(records, NoneCooling));
        Assert.Null(NationRotation.BestPerformer(Array.Empty<SegmentRecord>(), NoneCooling));
    }

    [Fact]
    public void ChooseRunsTheBestNationThenTheExploratoryOne()
    {
        var records = new[] { new SegmentRecord("players:France", 3, 2, 800, null) };

        Assert.Equal(new[] { "France", "Germany" }, NationRotation.Choose(records, Ring, "England", NoneCooling));
        Assert.Equal(new[] { "France" }, NationRotation.Choose(records, Ring, "Germany", NoneCooling));
        Assert.Equal(new[] { "England" }, NationRotation.Choose(Array.Empty<SegmentRecord>(), Ring, null, NoneCooling));
    }
}
