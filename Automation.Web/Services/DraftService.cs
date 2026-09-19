using Automation.Sbc;
using Automation.Sbc.Fulfilment;

namespace Automation.Web.Services;

public sealed class DraftService
{
    private readonly SbcStore store_;
    private readonly MySqlFulfilmentStore fulfilments_;
    private readonly DraftCache cache_;

    public DraftService(SbcStore store, MySqlFulfilmentStore fulfilments, DraftCache cache)
    {
        store_ = store;
        fulfilments_ = fulfilments;
        cache_ = cache;
    }

    public IReadOnlyList<DraftedChallenge> DraftAll(long budget)
    {
        var cached = cache_.Read();
        var result = cached.Count > 0 ? cached.Select(entry => entry.Drafted).ToList() : Regenerate(budget);

        return result;
    }

    public IReadOnlyList<DraftedChallenge> Regenerate(long budget)
    {
        var options = new SolveOptions(budget, Formations.DEFAULT_SQUAD_SIZE, ChemistryThresholds.Default,
            TeamLinks.None);
        var solved = SbcWorkbench.Draft(store_.ReadChallenges(), store_.ReadClubPlayers(), options);

        cache_.Write(solved, DateTime.UtcNow);

        return solved;
    }

    public DateTime? SolvedAt()
    {
        var cached = cache_.Read();

        return cached.Count > 0 ? cached[0].SolvedAt : null;
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

    public IReadOnlyList<SquadPlayer> ClubPlayers()
    {
        return store_.ReadEveryClubPlayer();
    }

    public IReadOnlyList<long> Exclusions()
    {
        return store_.ReadExclusions();
    }

    public void SetExcluded(long clubPlayerId, bool excluded)
    {
        if (excluded) store_.Exclude(clubPlayerId);
        else store_.Include(clubPlayerId);

        cache_.Invalidate();
    }

    public void RemoveApproval(int approvalId)
    {
        store_.RemoveApproval(approvalId);
    }

    public int CompletedApprovals()
    {
        return store_.CountCompletedApprovals();
    }

    public IReadOnlyList<ApprovalRecord> Approvals()
    {
        return store_.ReadApprovals();
    }
}
