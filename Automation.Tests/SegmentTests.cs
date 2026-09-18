using Automation.Trading;

namespace Automation.Tests;

public class SegmentTests
{
    [Fact]
    public void PlayerSegmentCarriesTheNation()
    {
        Assert.Equal("players:England", Segments.Players("England"));
        Assert.Equal("England", Segments.Nation("players:England"));
        Assert.Null(Segments.Nation("kits"));
    }

    [Fact]
    public void SnipeSegmentIsNamedAfterItsFilter()
    {
        Assert.Equal("players:om-silvers", Segments.Snipe("OM silvers"));
        Assert.Equal("players:psg-silvers", Segments.Snipe(" PSG silvers "));
    }
}
