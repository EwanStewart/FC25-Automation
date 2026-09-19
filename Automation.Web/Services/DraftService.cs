using Automation.Sbc;
using Automation.Sbc.Fulfilment;

namespace Automation.Web.Services;

public sealed class DraftService
{
    private readonly SbcStore store_;
    private readonly MySqlFulfilmentStore fulfilments_;

    public DraftService(SbcStore store, MySqlFulfilmentStore fulfilments)
    {
        store_ = store;
        fulfilments_ = fulfilments;
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
        var approvalId = store_.SaveApproval(drafted.Challenge, drafted.Squad);

        fulfilments_.Queue(approvalId, drafted.Challenge.ChallengeId, FulfilmentDefaults.DRY_RUN,
            drafted.Squad.EstimatedCost, FulfilmentGaps.From(drafted.Squad));

        return approvalId;
    }

    public IReadOnlyList<ApprovalRecord> Approvals()
    {
        return store_.ReadApprovals();
    }
}
