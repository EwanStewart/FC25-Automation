using Automation.Sbc;

namespace Automation.Web.Services;

public sealed class DraftService
{
    private readonly SbcStore store_;

    public DraftService(SbcStore store)
    {
        store_ = store;
    }

    public IReadOnlyList<DraftedChallenge> DraftAll(long budget)
    {
        var options = new SolveOptions(budget, Formations.DEFAULT_SQUAD_SIZE, ChemistryThresholds.Default,
            TeamLinks.None);

        return SbcWorkbench.Draft(store_.ReadChallenges(), store_.ReadClubPlayers(), options);
    }

    public DraftedChallenge? Draft(int challengeId, long budget)
    {
        return DraftAll(budget).FirstOrDefault(drafted => drafted.Challenge.ChallengeId == challengeId);
    }

    public int Approve(DraftedChallenge drafted)
    {
        return store_.SaveApproval(drafted.Challenge, drafted.Squad);
    }

    public IReadOnlyList<ApprovalRecord> Approvals()
    {
        return store_.ReadApprovals();
    }
}
