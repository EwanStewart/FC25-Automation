using Google.OrTools.Sat;

namespace Automation.Sbc;

public enum SolveOutcome
{
    Solved,
    Unsatisfiable,
    UnsupportedRequirement,
    UnknownFormation
}

public sealed record SolveOptions(
    long Budget,
    int SquadSize,
    ChemistryThresholds Thresholds,
    TeamLinks Links,
    double TimeLimitSeconds = 10.0,
    bool EnforceBudget = false);

public sealed record SquadSlot(int Index, string Position, SquadPlayer Player, MarketSpecification? Gap);

public sealed record SolvedSquad(
    SolveOutcome Outcome,
    string Detail,
    IReadOnlyList<SquadSlot> Slots,
    long EstimatedCost,
    int PurchaseCount,
    SquadAssessment? Assessment);

internal sealed record Candidate(SquadPlayer Player, MarketSpecification? Gap, int? FixedSlot, long Cost);

internal sealed class SquadModel
{
    public required CpModel Model { get; init; }
    public required IReadOnlyList<Candidate> Candidates { get; init; }
    public required IReadOnlyList<string> SlotPositions { get; init; }
    public required BoolVar[,] Placement { get; init; }
    public required IntVar[] SlotPoints { get; init; }
    public Dictionary<int, IntVar> ClubCounts { get; } = [];
    public Dictionary<int, IntVar> LeagueCounts { get; } = [];
    public Dictionary<int, IntVar> NationCounts { get; } = [];
    public Dictionary<int, IntVar> ClubPoints { get; } = [];
    public Dictionary<int, IntVar> LeaguePoints { get; } = [];
    public Dictionary<int, IntVar> NationPoints { get; } = [];

    public LinearExpr Used(int index)
    {
        return LinearExpr.Sum(Row(index));
    }

    public IEnumerable<BoolVar> Row(int index)
    {
        for (var slot = 0; slot < SlotPositions.Count; slot++) yield return Placement[index, slot];
    }

    public IEnumerable<BoolVar> Column(int slot)
    {
        for (var index = 0; index < Candidates.Count; index++) yield return Placement[index, slot];
    }
}

public static class SquadSolver
{
    private const int OWNED_COST_DIVISOR = 100;
    private const int UNVERIFIED_WEIGHT = 3;

    public static SolvedSquad Solve(ChallengeRequirements challenge, IReadOnlyList<SquadPlayer> owned,
        SolveOptions options)
    {
        var unmodellable = Unmodellable(challenge);
        SolvedSquad result;

        if (!Formations.IsKnown(challenge.Formation))
            result = Refused(SolveOutcome.UnknownFormation, $"formation {challenge.Formation} has no known layout");
        else if (unmodellable.Count > 0)
            result = Refused(SolveOutcome.UnsupportedRequirement,
                string.Join("; ", unmodellable.Select(requirement => requirement.Description)));
        else
            result = Run(challenge, owned, options);

        return result;
    }

    private static IReadOnlyList<SquadRequirement> Unmodellable(ChallengeRequirements challenge)
    {
        return challenge.Requirements.Where(requirement =>
            requirement.Kind is RequirementKind.Unsupported or RequirementKind.StarRating).ToList();
    }

    private static SolvedSquad Refused(SolveOutcome outcome, string detail)
    {
        return new SolvedSquad(outcome, detail, [], 0, 0, null);
    }

    private static SolvedSquad Run(ChallengeRequirements challenge, IReadOnlyList<SquadPlayer> owned,
        SolveOptions options)
    {
        var slotPositions = Formations.SlotPositions(challenge.Formation);
        var candidates = Candidates(challenge, owned, slotPositions);
        var squad = Assemble(candidates, slotPositions);

        AddPlacementRules(squad);
        AddBuckets(squad, options);
        AddChemistry(squad, options);
        AddRequirements(squad, challenge, options);
        AddBudget(squad, options);
        squad.Model.Minimize(LinearExpr.WeightedSum(
            Enumerable.Range(0, candidates.Count).Select(index => squad.Used(index)),
            candidates.Select(candidate => candidate.Cost)));

        return Read(squad, challenge, options);
    }

    private static IReadOnlyList<Candidate> Candidates(ChallengeRequirements challenge,
        IReadOnlyList<SquadPlayer> owned, IReadOnlyList<string> slotPositions)
    {
        List<Candidate> result = [];

        result.AddRange(owned.Where(player => Allowed(player, challenge)).Select(player =>
            new Candidate(player, null, null, Math.Max(1, player.MarketAverage / OWNED_COST_DIVISOR))));
        result.AddRange(MarketCandidates.Generate(challenge, owned, slotPositions, MarketReference.Ea)
            .Where(candidate => Allowed(candidate.Player, challenge)).Select(candidate =>
                new Candidate(candidate.Player, candidate.Specification, candidate.SlotIndex,
                    Preference(candidate.Specification))));

        return result;
    }

    private static long Preference(MarketSpecification specification)
    {
        return specification.Evidence == MarketEvidence.Observed
            ? specification.EstimatedCost
            : specification.EstimatedCost * UNVERIFIED_WEIGHT;
    }

    private static bool Allowed(SquadPlayer player, ChallengeRequirements challenge)
    {
        return challenge.Requirements.Where(requirement => requirement.Kind == RequirementKind.EveryPlayer)
            .All(requirement => SquadAssessor.Matches(player, requirement.Filters, requirement.Comparison));
    }

    private static SquadModel Assemble(IReadOnlyList<Candidate> candidates, IReadOnlyList<string> slotPositions)
    {
        var model = new CpModel();
        var slotPoints = Enumerable.Range(0, slotPositions.Count)
            .Select(slot => model.NewIntVar(0, ChemistryCalculator.SLOT_MAX_CHEMISTRY, $"chem{slot}")).ToArray();
        var placement = new BoolVar[candidates.Count, slotPositions.Count];

        for (var index = 0; index < candidates.Count; index++)
        for (var slot = 0; slot < slotPositions.Count; slot++)
            placement[index, slot] = model.NewBoolVar($"x{index}_{slot}");

        return new SquadModel
        {
            Model = model, Candidates = candidates, SlotPositions = slotPositions, Placement = placement,
            SlotPoints = slotPoints
        };
    }

    private static void AddPlacementRules(SquadModel squad)
    {
        for (var slot = 0; slot < squad.SlotPositions.Count; slot++)
            squad.Model.AddExactlyOne(squad.Column(slot));

        for (var index = 0; index < squad.Candidates.Count; index++) AddCandidateRules(squad, index);
    }

    private static void AddCandidateRules(SquadModel squad, int index)
    {
        var candidate = squad.Candidates[index];

        squad.Model.Add(squad.Used(index) <= 1);

        for (var slot = 0; slot < squad.SlotPositions.Count; slot++)
            if (!Playable(candidate, squad.SlotPositions[slot], slot))
                squad.Model.Add(squad.Placement[index, slot] == 0);
    }

    private static bool Playable(Candidate candidate, string position, int slot)
    {
        return (candidate.FixedSlot is null || candidate.FixedSlot == slot) &&
               candidate.Player.PossiblePositions.Contains(position, StringComparer.OrdinalIgnoreCase);
    }

    private static void AddBuckets(SquadModel squad, SolveOptions options)
    {
        AddBucket(squad, squad.ClubCounts, candidate => options.Links.Resolve(candidate.Player.TeamId),
            ChemistryCalculator.LEGENDS_CLUB_ID);
        AddBucket(squad, squad.LeagueCounts, candidate => candidate.Player.LeagueId,
            ChemistryCalculator.LEGENDS_LEAGUE_ID);
        AddBucket(squad, squad.NationCounts, candidate => candidate.Player.NationId, 0);
    }

    private static void AddBucket(SquadModel squad, Dictionary<int, IntVar> counts, Func<Candidate, int> key,
        int excluded)
    {
        foreach (var group in Enumerable.Range(0, squad.Candidates.Count)
                     .GroupBy(index => key(squad.Candidates[index]))
                     .Where(group => group.Key > 0 && group.Key != excluded))
        {
            var count = squad.Model.NewIntVar(0, squad.SlotPositions.Count, $"count{counts.Count}_{group.Key}");
            squad.Model.Add(count == LinearExpr.Sum(group.Select(squad.Used)));
            counts[group.Key] = count;
        }
    }

    private static void AddChemistry(SquadModel squad, SolveOptions options)
    {
        AddPoints(squad, squad.ClubCounts, squad.ClubPoints, options.Thresholds.Club);
        AddPoints(squad, squad.LeagueCounts, squad.LeaguePoints, options.Thresholds.League);
        AddPoints(squad, squad.NationCounts, squad.NationPoints, options.Thresholds.Nation);

        for (var slot = 0; slot < squad.SlotPositions.Count; slot++) AddSlotChemistry(squad, slot);
    }

    private static void AddPoints(SquadModel squad, Dictionary<int, IntVar> counts, Dictionary<int, IntVar> points,
        IReadOnlyList<ChemistryThreshold> thresholds)
    {
        foreach (var (key, count) in counts)
        {
            var reached = thresholds.Select(threshold => Reached(squad, count, threshold)).ToList();
            var total = squad.Model.NewIntVar(0, ChemistryCalculator.SLOT_MAX_CHEMISTRY, $"points{key}");

            squad.Model.Add(total == LinearExpr.WeightedSum(reached,
                thresholds.Select(threshold => (long)threshold.Points)));
            points[key] = total;
        }
    }

    private static BoolVar Reached(SquadModel squad, IntVar count, ChemistryThreshold threshold)
    {
        var reached = squad.Model.NewBoolVar($"reached{threshold.Requirement}");

        squad.Model.Add(count >= threshold.Requirement).OnlyEnforceIf(reached);
        squad.Model.Add(count <= threshold.Requirement - 1).OnlyEnforceIf(reached.Not());

        return reached;
    }

    private static void AddSlotChemistry(SquadModel squad, int slot)
    {
        var raw = squad.Model.NewIntVar(0, 4 * ChemistryCalculator.SLOT_MAX_CHEMISTRY, $"raw{slot}");

        for (var index = 0; index < squad.Candidates.Count; index++) AddCandidateChemistry(squad, slot, index, raw);

        squad.Model.AddMinEquality(squad.SlotPoints[slot],
            [raw, LinearExpr.Constant(ChemistryCalculator.SLOT_MAX_CHEMISTRY)]);
    }

    private static void AddCandidateChemistry(SquadModel squad, int slot, int index, IntVar raw)
    {
        if (Playable(squad.Candidates[index], squad.SlotPositions[slot], slot))
            squad.Model.Add(raw == SlotSum(squad, index)).OnlyEnforceIf(squad.Placement[index, slot]);
    }

    private static LinearExpr SlotSum(SquadModel squad, int index)
    {
        var player = squad.Candidates[index].Player;
        var maximum = player.TeamId is ChemistryCalculator.LEGENDS_CLUB_ID or ChemistryCalculator.LEAGUE_HERO_CLUB_ID
            or ChemistryCalculator.HALL_OF_FUT_CLUB_ID || player.LeagueId == ChemistryCalculator.LEGENDS_LEAGUE_ID;
        List<LinearExpr> parts =
        [
            LinearExpr.Constant(maximum ? ChemistryCalculator.SLOT_MAX_CHEMISTRY : 0),
            Points(squad.ClubPoints, player.TeamId), Points(squad.LeaguePoints, player.LeagueId),
            Points(squad.NationPoints, player.NationId)
        ];

        return LinearExpr.Sum(parts);
    }

    private static LinearExpr Points(Dictionary<int, IntVar> points, int key)
    {
        return points.TryGetValue(key, out var variable) ? variable : LinearExpr.Constant(0);
    }

    private static void AddRequirements(SquadModel squad, ChallengeRequirements challenge, SolveOptions options)
    {
        foreach (var requirement in challenge.Requirements) AddRequirement(squad, requirement, options);
    }

    private static void AddRequirement(SquadModel squad, SquadRequirement requirement, SolveOptions options)
    {
        switch (requirement.Kind)
        {
            case RequirementKind.PlayerCount:
            case RequirementKind.PlayerLevelCount:
                AddCount(squad, MatchingUse(squad, requirement), requirement.Value, requirement.Comparison);
                break;
            case RequirementKind.SquadRating:
                AddRating(squad, requirement);
                break;
            case RequirementKind.TotalChemistry:
                AddCount(squad, LinearExpr.Sum(squad.SlotPoints), requirement.Value, requirement.Comparison);
                break;
            case RequirementKind.PlayerChemistry:
                AddPlayerChemistry(squad, requirement);
                break;
            case RequirementKind.DistinctClubs:
                AddDistinct(squad, squad.ClubCounts, requirement);
                break;
            case RequirementKind.DistinctLeagues:
                AddDistinct(squad, squad.LeagueCounts, requirement);
                break;
            case RequirementKind.DistinctNations:
                AddDistinct(squad, squad.NationCounts, requirement);
                break;
            case RequirementKind.SameClubCount:
                AddLargest(squad, squad.ClubCounts, requirement);
                break;
            case RequirementKind.SameLeagueCount:
                AddLargest(squad, squad.LeagueCounts, requirement);
                break;
            case RequirementKind.SameNationCount:
                AddLargest(squad, squad.NationCounts, requirement);
                break;
            case RequirementKind.LegendCount:
                AddCount(squad, LegendUse(squad), requirement.Value, requirement.Comparison);
                break;
        }
    }

    private static LinearExpr MatchingUse(SquadModel squad, SquadRequirement requirement)
    {
        var matching = Enumerable.Range(0, squad.Candidates.Count).Where(index =>
            SquadAssessor.Matches(squad.Candidates[index].Player, requirement.Filters,
                RequirementComparison.Exact)).ToList();

        return matching.Count == 0 ? LinearExpr.Constant(0) : LinearExpr.Sum(matching.Select(squad.Used));
    }

    private static LinearExpr LegendUse(SquadModel squad)
    {
        var legends = Enumerable.Range(0, squad.Candidates.Count).Where(index =>
            squad.Candidates[index].Player.TeamId == ChemistryCalculator.LEGENDS_CLUB_ID ||
            squad.Candidates[index].Player.LeagueId == ChemistryCalculator.LEGENDS_LEAGUE_ID).ToList();

        return legends.Count == 0 ? LinearExpr.Constant(0) : LinearExpr.Sum(legends.Select(squad.Used));
    }

    private static void AddRating(SquadModel squad, SquadRequirement requirement)
    {
        var ratings = LinearExpr.WeightedSum(
            Enumerable.Range(0, squad.Candidates.Count).Select(squad.Used),
            squad.Candidates.Select(candidate => (long)candidate.Player.Rating));

        AddCount(squad, ratings, requirement.Value * squad.SlotPositions.Count, requirement.Comparison);
    }

    private static void AddPlayerChemistry(SquadModel squad, SquadRequirement requirement)
    {
        foreach (var slot in squad.SlotPoints) AddCount(squad, slot, requirement.Value, requirement.Comparison);
    }

    private static void AddDistinct(SquadModel squad, Dictionary<int, IntVar> counts, SquadRequirement requirement)
    {
        var present = counts.Values.Select(count => Present(squad, count)).ToList();

        AddCount(squad, present.Count == 0 ? LinearExpr.Constant(0) : LinearExpr.Sum(present), requirement.Value,
            requirement.Comparison);
    }

    private static BoolVar Present(SquadModel squad, IntVar count)
    {
        var present = squad.Model.NewBoolVar("present");

        squad.Model.Add(count >= 1).OnlyEnforceIf(present);
        squad.Model.Add(count == 0).OnlyEnforceIf(present.Not());

        return present;
    }

    private static void AddLargest(SquadModel squad, Dictionary<int, IntVar> counts, SquadRequirement requirement)
    {
        var largest = squad.Model.NewIntVar(0, squad.SlotPositions.Count, "largest");

        squad.Model.AddMaxEquality(largest, counts.Values.Select(count => (LinearExpr)count));
        AddCount(squad, largest, requirement.Value, requirement.Comparison);
    }

    private static void AddCount(SquadModel squad, LinearExpr actual, int value, RequirementComparison comparison)
    {
        switch (comparison)
        {
            case RequirementComparison.Maximum:
                squad.Model.Add(actual <= value);
                break;
            case RequirementComparison.Exact:
                squad.Model.Add(actual == value);
                break;
            default:
                squad.Model.Add(actual >= value);
                break;
        }
    }

    private static void AddBudget(SquadModel squad, SolveOptions options)
    {
        var purchases = Enumerable.Range(0, squad.Candidates.Count)
            .Where(index => squad.Candidates[index].Gap is not null).ToList();

        if (options.EnforceBudget && purchases.Count > 0)
            squad.Model.Add(LinearExpr.WeightedSum(purchases.Select(squad.Used),
                purchases.Select(index => squad.Candidates[index].Cost)) <= options.Budget);
    }

    private static SolvedSquad Read(SquadModel squad, ChallengeRequirements challenge, SolveOptions options)
    {
        var solver = new CpSolver { StringParameters = $"max_time_in_seconds:{options.TimeLimitSeconds}" };
        var status = solver.Solve(squad.Model);
        SolvedSquad result;

        if (status is CpSolverStatus.Optimal or CpSolverStatus.Feasible)
            result = Extract(squad, solver, challenge, options);
        else
            result = Refused(SolveOutcome.Unsatisfiable,
                $"no squad satisfies the challenge from the club plus permitted purchases ({status}) {squad.Model.Validate()}");

        return result;
    }

    private static SolvedSquad Extract(SquadModel squad, CpSolver solver, ChallengeRequirements challenge,
        SolveOptions options)
    {
        var slots = Enumerable.Range(0, squad.SlotPositions.Count).Select(slot => Slot(squad, solver, slot)).ToList();
        var players = slots.Select(slot => slot.Player).ToList();
        var purchases = slots.Where(slot => slot.Gap is not null).ToList();
        var assessment = SquadAssessor.Assess(challenge, players, null, options.Thresholds, options.Links);

        return new SolvedSquad(SolveOutcome.Solved, "solved", slots,
            purchases.Sum(slot => (long)slot.Gap!.EstimatedCost), purchases.Count, assessment);
    }

    private static SquadSlot Slot(SquadModel squad, CpSolver solver, int slot)
    {
        var index = Enumerable.Range(0, squad.Candidates.Count)
            .First(candidate => solver.BooleanValue(squad.Placement[candidate, slot]));
        var candidate = squad.Candidates[index];

        return new SquadSlot(slot, squad.SlotPositions[slot], candidate.Player, candidate.Gap);
    }
}
