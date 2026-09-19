using Automation.Sbc;

namespace Automation.Tests;

public class SbcRequirementTextTests
{
    private static readonly NameLookup NAMES = new(
        new Dictionary<string, int> { ["Scotland"] = 42 },
        new Dictionary<string, int> { ["Premier League"] = 13 },
        new Dictionary<string, int> { ["Celtic"] = 145 });

    [Fact]
    public void ReadsTheSquadSize()
    {
        var requirement = Single("Number of Players in the Squad: 11");

        Assert.Equal(RequirementKind.PlayerCount, requirement.Kind);
        Assert.Equal(RequirementComparison.Exact, requirement.Comparison);
        Assert.Equal(11, requirement.Value);
        Assert.Empty(requirement.Filters);
    }

    [Fact]
    public void ReadsATotalChemistryFloor()
    {
        var requirement = Single("Total Chemistry: Min. 14");

        Assert.Equal(RequirementKind.TotalChemistry, requirement.Kind);
        Assert.Equal(RequirementComparison.Minimum, requirement.Comparison);
        Assert.Equal(14, requirement.Value);
    }

    [Fact]
    public void ReadsADistinctClubCount()
    {
        var requirement = Single("Clubs in Squad: Min. 2");

        Assert.Equal(RequirementKind.DistinctClubs, requirement.Kind);
        Assert.Equal(2, requirement.Value);
    }

    [Theory]
    [InlineData("Player Quality: Min. Bronze", RequirementComparison.Minimum, PlayerQuality.Bronze)]
    [InlineData("Player Quality: Exactly Silver", RequirementComparison.Exact, PlayerQuality.Silver)]
    public void ReadsASquadWideQuality(string line, RequirementComparison comparison, PlayerQuality quality)
    {
        var requirement = Single(line);

        Assert.Equal(RequirementKind.EveryPlayer, requirement.Kind);
        Assert.Equal(comparison, requirement.Comparison);
        Assert.Equal(PlayerFilterKind.Quality, requirement.Filters.Single().Kind);
        Assert.Equal((int)quality, requirement.Filters.Single().Value);
    }

    [Fact]
    public void ReadsACountOfPlayersOfATier()
    {
        var requirement = Single("Silver: Min. 1 Players");

        Assert.Equal(RequirementKind.PlayerLevelCount, requirement.Kind);
        Assert.Equal(RequirementComparison.Minimum, requirement.Comparison);
        Assert.Equal(1, requirement.Value);
        Assert.Equal(PlayerFilterKind.Level, requirement.Filters.Single().Kind);
        Assert.Equal((int)PlayerQuality.Silver, requirement.Filters.Single().Value);
    }

    [Theory]
    [InlineData("Gold: Exactly 11 Players", RequirementComparison.Exact, 11, PlayerQuality.Gold)]
    [InlineData("Bronze: Min. 11 Players", RequirementComparison.Minimum, 11, PlayerQuality.Bronze)]
    [InlineData("Gold: Max. 3 Players", RequirementComparison.Maximum, 3, PlayerQuality.Gold)]
    public void ReadsTheMinimumAndExactTierForms(string line, RequirementComparison comparison, int value,
        PlayerQuality quality)
    {
        var requirement = Single(line);

        Assert.Equal(RequirementKind.PlayerLevelCount, requirement.Kind);
        Assert.Equal(comparison, requirement.Comparison);
        Assert.Equal(value, requirement.Value);
        Assert.Equal((int)quality, requirement.Filters.Single().Value);
    }

    [Fact]
    public void ATierCountAndASquadWideFloorReadAsSeparateRequirements()
    {
        var requirements = RequirementTextParser.Parse(["Silver: Min. 1 Players", "Player Quality: Min. Bronze"],
            NAMES);

        Assert.Equal(RequirementKind.PlayerLevelCount, requirements[0].Kind);
        Assert.Equal(PlayerFilterKind.Level, requirements[0].Filters.Single().Kind);
        Assert.Equal(RequirementKind.EveryPlayer, requirements[1].Kind);
        Assert.Equal(PlayerFilterKind.Quality, requirements[1].Filters.Single().Kind);
    }

    [Fact]
    public void ResolvesANationByName()
    {
        var requirement = Single("Scotland: Min. 1 Player");

        Assert.Equal(RequirementKind.PlayerCount, requirement.Kind);
        Assert.Equal(PlayerFilterKind.Nation, requirement.Filters.Single().Kind);
        Assert.Equal(42, requirement.Filters.Single().Value);
        Assert.Equal("Scotland", requirement.Filters.Single().Label);
    }

    [Fact]
    public void AnUnknownNameIsUnsupported()
    {
        var requirement = Single("Narnia: Min. 1 Player");

        Assert.Equal(RequirementKind.Unsupported, requirement.Kind);
    }

    [Fact]
    public void AnUnreadableLineIsUnsupported()
    {
        var requirement = Single("Something entirely new");

        Assert.Equal(RequirementKind.Unsupported, requirement.Kind);
    }

    [Fact]
    public void BlankLinesAreDropped()
    {
        Assert.Empty(RequirementTextParser.Parse(["", "   "], NAMES));
    }

    [Fact]
    public void ReadsTheWholeObservedRequirementSet()
    {
        string[] lines =
        [
            "Scotland: Min. 1 Player", "Clubs in Squad: Min. 2", "Silver: Min. 1 Players",
            "Player Quality: Min. Bronze", "Total Chemistry: Min. 14", "Player Quality: Exactly Silver",
            "Number of Players in the Squad: 11"
        ];
        var requirements = RequirementTextParser.Parse(lines, NAMES);

        Assert.Equal(7, requirements.Count);
        Assert.DoesNotContain(requirements, requirement => requirement.Kind == RequirementKind.Unsupported);
    }

    private static SquadRequirement Single(string line)
    {
        return RequirementTextParser.Parse([line], NAMES).Single();
    }
}
