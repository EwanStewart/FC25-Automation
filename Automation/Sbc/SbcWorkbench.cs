namespace Automation.Sbc;

public sealed record DraftedChallenge(ChallengeRequirements Challenge, SolvedSquad Squad);

public static class SbcWorkbench
{
    public static IReadOnlyList<DraftedChallenge> Draft(IReadOnlyList<ChallengeRequirements> challenges,
        IReadOnlyList<SquadPlayer> club, SolveOptions options)
    {
        return challenges.Select(challenge => new DraftedChallenge(challenge, SquadSolver.Solve(challenge, club,
            options))).ToList();
    }

    public static IReadOnlyList<DraftedChallenge> Solvable(IReadOnlyList<DraftedChallenge> drafted)
    {
        return drafted.Where(entry => entry.Squad.Outcome == SolveOutcome.Solved).ToList();
    }
}
