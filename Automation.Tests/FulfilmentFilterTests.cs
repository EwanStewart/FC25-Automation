using Automation.Sbc.Fulfilment;

namespace Automation.Tests;

public class FulfilmentFilterTests
{
    private const string POSITION_IMAGE =
        "https://www.ea.com/ea-sports-fc/ultimate-team/web-app/content/2027/fut/items/images/mobile/positions/3.png";

    private static FilterControl Control(int index, string label, string image, bool clearable)
    {
        return new FilterControl(index, label, image, clearable);
    }

    private static IReadOnlyList<FilterControl> Panel()
    {
        return
        [
            Control(0, "My Club", "images/SearchFilters/players_club.png", false),
            Control(1, "Quality", "images/SearchFilters/level/any.png", false),
            Control(2, "Rarity", "images/SearchFilters/rarity/any.png", false),
            Control(3, "RB", POSITION_IMAGE, true),
            Control(4, "Chemistry Style", "images/mobile/chemistrystyles/list/default.png", false),
            Control(5, "Country/Region", "images/mobile/flags/dark/default.png", false),
            Control(6, "League", "images/mobile/leagues/dark/default.png", false)
        ];
    }

    [Fact]
    public void ThePositionFilterIsTheClearableControlCarryingAPositionImage()
    {
        Assert.Equal(3, ClubSearchFilters.PositionControl(Panel()));
    }

    [Fact]
    public void APositionFilterAlreadyClearedOffersNothingToClear()
    {
        var panel = Panel().Select(control => control.Index == 3 ? control with { Clearable = false } : control)
            .ToList();

        Assert.Equal(ClubSearchFilters.NOTHING, ClubSearchFilters.PositionControl(panel));
    }

    [Fact]
    public void NoFilterPanelAtAllNamesNoControl()
    {
        Assert.Equal(ClubSearchFilters.NOTHING, ClubSearchFilters.PositionControl([]));
    }

    [Fact]
    public void AClearableFilterThatIsNotThePositionOneIsLeftAlone()
    {
        var panel = Panel().Select(control => control with { Clearable = true })
            .Where(control => control.Index != 3).ToList();

        Assert.Equal(ClubSearchFilters.NOTHING, ClubSearchFilters.PositionControl(panel));
    }

    [Fact]
    public void APositionFilterWearingAForbiddenLabelIsRefusedRatherThanClicked()
    {
        List<FilterControl> panel = [Control(0, "Send to Transfer List", POSITION_IMAGE, true)];

        Assert.Throws<InvalidOperationException>(() => ClubSearchFilters.PositionControl(panel));
    }
}
