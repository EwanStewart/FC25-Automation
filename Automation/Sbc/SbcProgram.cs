namespace Automation.Sbc;

public static class SbcProgram
{
    public const long DEFAULT_BUDGET = 20000;

    public static void ImportChallenges(string path)
    {
        var body = File.ReadAllText(path);
        var challenges = RequirementParser.Parse(body);

        new SbcStore().SaveChallenges(challenges, body);
        Console.WriteLine($"stored {challenges.Count} challenges from {path}");
    }

    public static IReadOnlyList<DraftedChallenge> Draft(long budget)
    {
        var store = new SbcStore();
        var options = new SolveOptions(budget, Formations.DEFAULT_SQUAD_SIZE, ChemistryThresholds.Default,
            TeamLinks.None);

        return SbcWorkbench.Draft(store.ReadChallenges(), store.ReadClubPlayers(), options);
    }

    public static SolveSummary Summarise(long budget)
    {
        return SbcSolveSummary.Summarise(Draft(budget));
    }

    public static void ReportSummary(long budget)
    {
        var summary = Summarise(budget);

        Console.WriteLine($"challenges imported: {summary.Challenges}");
        Console.WriteLine($"solvable from owned players alone: {summary.SolvedFromOwnedPlayers}");
        Console.WriteLine(
            $"solvable after buying: {summary.SolvedWithPurchases} needing {summary.Purchases} cards for about {summary.PurchaseCost} coins");
        Console.WriteLine($"unsolvable: {summary.Unsolvable}");
        Console.WriteLine($"refused for an unsupported requirement: {summary.Refused}");

        foreach (var unsupported in summary.UnsupportedTypes)
            Console.WriteLine(
                $"  {unsupported.Type}: {unsupported.Occurrences} occurrences across {unsupported.Challenges} challenges");
    }

    public static void Report(long budget)
    {
        foreach (var drafted in Draft(budget)) ReportOne(drafted);
    }

    private static void ReportOne(DraftedChallenge drafted)
    {
        Console.WriteLine(
            $"{drafted.Challenge.ChallengeId} {drafted.Challenge.Name} [{drafted.Challenge.Formation}] {drafted.Squad.Outcome} {drafted.Squad.Detail}");

        foreach (var slot in drafted.Squad.Slots) ReportSlot(drafted, slot);

        foreach (var outcome in drafted.Squad.Assessment?.Outcomes ?? [])
            Console.WriteLine($"    [{(outcome.Passed ? "pass" : "fail")}] {outcome.Requirement.Description} (actual {outcome.Actual})");
    }

    private static void ReportSlot(DraftedChallenge drafted, SquadSlot slot)
    {
        var chemistry = drafted.Squad.Assessment?.Chemistry.SlotPoints[slot.Index] ?? 0;
        var source = slot.Player.Owned ? "owned" : $"buy {slot.Gap?.EstimatedCost} coins";

        Console.WriteLine(
            $"  {slot.Index,2} {slot.Position,-3} {slot.Player.Rating,3} {slot.Player.Name,-32} chem {chemistry} {source}");
    }
}
