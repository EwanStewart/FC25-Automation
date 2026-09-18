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
}
