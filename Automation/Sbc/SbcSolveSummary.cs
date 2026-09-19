namespace Automation.Sbc;

public sealed record UnsupportedRequirementCount(string Type, int Challenges, int Occurrences);

public sealed record SolveSummary(
    int Challenges,
    int SolvedFromOwnedPlayers,
    int SolvedWithPurchases,
    int Purchases,
    long PurchaseCost,
    int Unsolvable,
    int Refused,
    IReadOnlyList<UnsupportedRequirementCount> UnsupportedTypes);

public static class SbcSolveSummary
{
    private const string UNNAMED_TYPE = "unnamed";

    public static SolveSummary Summarise(IReadOnlyList<DraftedChallenge> drafted)
    {
        var solved = drafted.Where(entry => entry.Squad.Outcome == SolveOutcome.Solved).ToList();
        var bought = solved.Where(entry => entry.Squad.PurchaseCount > 0).ToList();

        return new SolveSummary(
            drafted.Count,
            solved.Count - bought.Count,
            bought.Count,
            bought.Sum(entry => entry.Squad.PurchaseCount),
            bought.Sum(entry => entry.Squad.EstimatedCost),
            drafted.Count(entry => entry.Squad.Outcome is SolveOutcome.Unsatisfiable or SolveOutcome.UnknownFormation),
            drafted.Count(entry => entry.Squad.Outcome == SolveOutcome.UnsupportedRequirement),
            UnsupportedTypes(drafted));
    }

    private static IReadOnlyList<UnsupportedRequirementCount> UnsupportedTypes(
        IReadOnlyList<DraftedChallenge> drafted)
    {
        return drafted.SelectMany(NamedUnsupported).GroupBy(entry => entry.Type)
            .Select(group => new UnsupportedRequirementCount(group.Key,
                group.Select(entry => entry.ChallengeId).Distinct().Count(), group.Count()))
            .OrderByDescending(entry => entry.Occurrences).ThenBy(entry => entry.Type).ToList();
    }

    private static IEnumerable<(string Type, int ChallengeId)> NamedUnsupported(DraftedChallenge drafted)
    {
        return drafted.Challenge.Requirements
            .Where(requirement => requirement.Kind == RequirementKind.Unsupported)
            .Select(requirement => (TypeName(requirement), drafted.Challenge.ChallengeId));
    }

    private static string TypeName(SquadRequirement requirement)
    {
        return requirement.Subject.Length > 0 ? requirement.Subject : UNNAMED_TYPE;
    }
}
