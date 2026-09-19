using Automation.Sbc;

namespace Automation.Tests;

public class SbcWorkbenchTests
{
    private static readonly string[] POSITIONS =
        ["GK", "RB", "CB", "CB", "LB", "RM", "CM", "CM", "LM", "ST", "ST"];

    private static IReadOnlyList<SquadPlayer> Club()
    {
        return POSITIONS.Select((position, index) =>
            new SquadPlayer(index + 1, index + 1, $"Owned {index}", 70, position, [position], 5, 13, 14, 0, true,
                300, true)).ToList();
    }

    private static SolveOptions Options()
    {
        return new SolveOptions(10000, 11, ChemistryThresholds.Default, TeamLinks.None);
    }

    [Fact]
    public void DraftsOneSquadPerChallenge()
    {
        var size = new SquadRequirement(RequirementKind.PlayerCount, RequirementComparison.Exact, 11, [], "size");
        ChallengeRequirements[] challenges =
        [
            new(1, 1, "First", "f442", [size]),
            new(2, 1, "Second", "f442", [size])
        ];

        var drafted = SbcWorkbench.Draft(challenges, Club(), Options());

        Assert.Equal(2, drafted.Count);
        Assert.Equal([1, 2], drafted.Select(entry => entry.Challenge.ChallengeId));
    }

    [Fact]
    public void SolvableDropsTheChallengesThatCouldNotBeDrafted()
    {
        var size = new SquadRequirement(RequirementKind.PlayerCount, RequirementComparison.Exact, 11, [], "size");
        var unsupported = new SquadRequirement(RequirementKind.Unsupported, RequirementComparison.Minimum, 1, [],
            "Unsupported requirement");
        ChallengeRequirements[] challenges =
        [
            new(1, 1, "First", "f442", [size]),
            new(2, 1, "Second", "f442", [size, unsupported])
        ];

        var solvable = SbcWorkbench.Solvable(SbcWorkbench.Draft(challenges, Club(), Options()));

        Assert.Equal(1, solvable.Single().Challenge.ChallengeId);
    }
}
