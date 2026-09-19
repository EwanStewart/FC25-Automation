using Automation.Sbc;

namespace Automation.Tests;

public class ChemistryCalculatorTests
{
    private static readonly IReadOnlyList<ChemistryPlayer> ACTIVE_SQUAD =
    [
        new(116021, 2215, 47, ["GK"], 0),
        new(131386, 2215, 21, ["RB"], 0),
        new(115996, 2215, 21, ["CB"], 0),
        new(38, 19, 21, ["CB"], 0),
        new(781, 80, 4, ["LB", "LM", "LW"], 0),
        new(10030, 19, 21, ["RB", "CB", "RM"], 0),
        new(21, 19, 21, ["LB", "CDM", "CM"], 0),
        new(112172, 19, 21, ["CDM", "CM"], 0),
        new(112172, 19, 18, ["RM", "LM", "RW", "ST", "LW"], 0),
        new(22, 19, 38, ["LM", "CAM", "ST", "LW"], 0),
        new(480, 53, 45, ["ST"], 0)
    ];

    private static readonly ChemistryPlayer ACTIVE_MANAGER = new(0, 353, 52, [], 0);

    private static IReadOnlyList<string> Formation442 => Formations.SlotPositions("f442");

    [Fact]
    public void AnEmptySquadScoresNothing()
    {
        var result = ChemistryCalculator.Calculate(Formation442, [null, null, null, null, null, null, null, null,
            null, null, null], null, ChemistryThresholds.Default, TeamLinks.None);

        Assert.Equal(0, result.Total);
    }

    [Fact]
    public void AWholeSquadOfOneClubTakesTheFullThirtyThree()
    {
        var squad = Formation442.Select(position => new ChemistryPlayer(5, 13, 14, [position], 0)).ToList();

        var result = ChemistryCalculator.Calculate(Formation442, squad, null, ChemistryThresholds.Default,
            TeamLinks.None);

        Assert.Equal(33, result.Total);
        Assert.All(result.SlotPoints, points => Assert.Equal(3, points));
    }

    [Fact]
    public void APlayerOutOfPositionScoresNothingAndFeedsNoThreshold()
    {
        List<ChemistryPlayer?> squad = [.. Formation442.Select(position =>
            (ChemistryPlayer?)new ChemistryPlayer(5, 13, 14, [position], 0))];
        squad[10] = new ChemistryPlayer(5, 13, 14, ["GK"], 0);

        var result = ChemistryCalculator.Calculate(Formation442, squad, null, ChemistryThresholds.Default,
            TeamLinks.None);

        Assert.Equal(0, result.SlotPoints[10]);
        Assert.Equal(30, result.Total);
    }

    [Fact]
    public void ASlotNeverScoresMoreThanThree()
    {
        var squad = Formation442.Select(position => new ChemistryPlayer(5, 13, 14, [position], 0)).ToList();

        var result = ChemistryCalculator.Calculate(Formation442, squad, null, ChemistryThresholds.Default,
            TeamLinks.None);

        Assert.All(result.SlotPoints, points => Assert.InRange(points, 0, ChemistryCalculator.SLOT_MAX_CHEMISTRY));
    }

    [Fact]
    public void TheManagerFeedsItsLeagueAndNationButNotItsClub()
    {
        List<ChemistryPlayer?> squad = [.. Formation442.Select(position =>
            (ChemistryPlayer?)new ChemistryPlayer(5, 13, 14, [position], 0))];
        squad[0] = new ChemistryPlayer(9, 77, 99, ["GK"], 0);
        var manager = new ChemistryPlayer(9, 77, 99, [], 0);

        var withManager = ChemistryCalculator.Calculate(Formation442, squad, manager, ChemistryThresholds.Default,
            TeamLinks.None);
        var without = ChemistryCalculator.Calculate(Formation442, squad, null, ChemistryThresholds.Default,
            TeamLinks.None);

        Assert.Equal(0, without.SlotPoints[0]);
        Assert.Equal(1, withManager.SlotPoints[0]);
    }

    [Fact]
    public void ScoresTheObservedActiveSquadOnePointShortOfTheReportedTotal()
    {
        var result = ChemistryCalculator.Calculate(Formation442, ACTIVE_SQUAD, ACTIVE_MANAGER,
            ChemistryThresholds.Default, TeamLinks.None);

        Assert.Equal(24, result.Total);
    }

    [Fact]
    public void AClubLinkOnTheGoalkeeperReachesTheReportedTotal()
    {
        var links = new TeamLinks(new Dictionary<int, int> { [116021] = 21 });

        var result = ChemistryCalculator.Calculate(Formation442, ACTIVE_SQUAD, ACTIVE_MANAGER,
            ChemistryThresholds.Default, links);

        Assert.Equal(25, result.Total);
    }

    [Fact]
    public void AnIconTakesTheFullThreeInPosition()
    {
        var result = ChemistryCalculator.Calculate(Formation442, SquadWithIcon(), null, ChemistryThresholds.Default,
            TeamLinks.None);

        Assert.Equal(3, result.SlotPoints[9]);
    }

    [Fact]
    public void AnIconLiftsEveryLeagueInTheSquad()
    {
        var withIcon = ChemistryCalculator.Calculate(Formation442, SquadWithIcon(), null,
            ChemistryThresholds.Default, TeamLinks.None);
        var squad = SquadWithIcon();
        squad[9] = new ChemistryPlayer(5, 13, 14, ["ST"], 0);
        var without = ChemistryCalculator.Calculate(Formation442, squad, null, ChemistryThresholds.Default,
            TeamLinks.None);

        Assert.Equal(0, without.SlotPoints[0]);
        Assert.Equal(1, withIcon.SlotPoints[0]);
        Assert.Equal(1, withIcon.SlotPoints[1]);
    }

    private static List<ChemistryPlayer?> SquadWithIcon()
    {
        List<ChemistryPlayer?> squad = [.. Formation442.Select(position =>
            (ChemistryPlayer?)new ChemistryPlayer(5, 13, 14, [position], 0))];
        squad[0] = new ChemistryPlayer(9, 77, 99, ["GK"], 0);
        squad[1] = new ChemistryPlayer(8, 77, 98, ["RB"], 0);
        squad[9] = new ChemistryPlayer(ChemistryCalculator.LEGENDS_CLUB_ID, ChemistryCalculator.LEGENDS_LEAGUE_ID, 14,
            ["ST"], 0);

        return squad;
    }
}
