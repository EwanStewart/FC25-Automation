using System.Text.Json;

namespace Automation.Sbc;

public enum SbcCaptureOutcome
{
    Captured,
    NotObserved,
    NoItems,
    Failed
}

public sealed record SbcResponse(int Status, string Url, string Body);

public sealed record SbcSet(
    int SetId,
    string Name,
    string Description,
    int CategoryId,
    string CategoryName,
    int Priority,
    long EndTime,
    int ChallengesCount,
    int ChallengesCompletedCount,
    bool Repeatable,
    int TimesCompleted)
{
    public bool Completed => ChallengesCount > 0 && ChallengesCompletedCount >= ChallengesCount;
}

public sealed record SbcChallengeRecord(
    int ChallengeId,
    int SetId,
    string Name,
    string Description,
    string Formation,
    string Status,
    string Type,
    string ElgOperation,
    string ChallengeImageId,
    int ContentId,
    int Priority,
    long EndTime,
    bool Repeatable,
    int TimesCompleted,
    int Tutorial,
    string Eligibility,
    string EligibilityDescription,
    string Body);

public sealed record SbcCatalogueReading(
    SbcCaptureOutcome Outcome,
    IReadOnlyList<SbcSet> Sets,
    IReadOnlyList<SbcChallengeRecord> Challenges,
    IReadOnlyList<int> MissingSets,
    string Detail);

public static class SbcCatalogue
{
    private const int OK_STATUS = 200;

    public static SbcCatalogueReading Read(IReadOnlyList<SbcResponse> setResponses,
        IReadOnlyList<SbcResponse> challengeResponses)
    {
        var failures = Failures(setResponses).Concat(Failures(challengeResponses)).ToList();
        var sets = MergeSets(setResponses);
        var challenges = MergeChallenges(challengeResponses);
        var missing = Missing(sets, challenges);
        var bodies = Bodies(setResponses).Concat(Bodies(challengeResponses)).Count();
        SbcCatalogueReading result;

        if (bodies == 0 && failures.Count == 0)
            result = new SbcCatalogueReading(SbcCaptureOutcome.NotObserved, [], [], [],
                "no SBC response was observed");
        else if (sets.Count > 0)
            result = new SbcCatalogueReading(SbcCaptureOutcome.Captured, sets, challenges, missing,
                Detail(sets.Count, challenges.Count, missing, failures));
        else if (failures.Count > 0)
            result = new SbcCatalogueReading(SbcCaptureOutcome.Failed, [], [], [],
                $"SBC responses returned {string.Join(", ", failures)}");
        else
            result = new SbcCatalogueReading(SbcCaptureOutcome.NoItems, [], challenges, [],
                "the SBC set response carried no sets");

        return result;
    }

    private static IEnumerable<int> Failures(IEnumerable<SbcResponse> responses)
    {
        return responses.Where(response => response.Status != OK_STATUS).Select(response => response.Status);
    }

    private static IEnumerable<SbcResponse> Bodies(IEnumerable<SbcResponse> responses)
    {
        return responses.Where(response => response.Status == OK_STATUS && response.Body.Length > 0);
    }

    private static IReadOnlyList<SbcSet> MergeSets(IEnumerable<SbcResponse> responses)
    {
        return Bodies(responses).SelectMany(response => ParseSets(response.Body)).GroupBy(entry => entry.SetId)
            .Select(group => group.Last()).OrderBy(entry => entry.SetId).ToList();
    }

    private static IReadOnlyList<SbcChallengeRecord> MergeChallenges(IEnumerable<SbcResponse> responses)
    {
        return Bodies(responses).SelectMany(response => ParseChallenges(response.Body))
            .GroupBy(entry => entry.ChallengeId).Select(group => group.Last())
            .OrderBy(entry => entry.SetId).ThenBy(entry => entry.ChallengeId).ToList();
    }

    private static IReadOnlyList<int> Missing(IReadOnlyList<SbcSet> sets,
        IReadOnlyList<SbcChallengeRecord> challenges)
    {
        var seen = challenges.Select(challenge => challenge.SetId).ToHashSet();

        return sets.Where(entry => !entry.Completed && !seen.Contains(entry.SetId)).Select(entry => entry.SetId)
            .ToList();
    }

    private static string Detail(int sets, int challenges, IReadOnlyList<int> missing, IReadOnlyList<int> failures)
    {
        var absent = missing.Count == 0 ? string.Empty : $", no challenges seen for sets {string.Join(", ", missing)}";
        var failed = failures.Count == 0 ? string.Empty : $", failed responses {string.Join(", ", failures)}";

        return $"{sets} sets and {challenges} challenges{absent}{failed}";
    }

    public static IReadOnlyList<SbcSet> ParseSets(string json)
    {
        List<SbcSet> result = [];

        try
        {
            using var document = JsonDocument.Parse(json);

            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("categories", out var categories) &&
                categories.ValueKind == JsonValueKind.Array)
                result = categories.EnumerateArray().SelectMany(ReadCategory).ToList();
        }
        catch (JsonException)
        {
        }

        return result;
    }

    private static IEnumerable<SbcSet> ReadCategory(JsonElement category)
    {
        IEnumerable<SbcSet> result = [];

        if (category.TryGetProperty("sets", out var sets) && sets.ValueKind == JsonValueKind.Array)
            result = sets.EnumerateArray().Select(entry => ReadSet(entry, category)).ToList();

        return result;
    }

    private static SbcSet ReadSet(JsonElement set, JsonElement category)
    {
        return new SbcSet(
            Number(set, "setId"),
            Text(set, "name"),
            Text(set, "description"),
            Number(category, "categoryId"),
            Text(category, "name"),
            Number(set, "priority"),
            Identifier(set, "endTime"),
            Number(set, "challengesCount"),
            Number(set, "challengesCompletedCount"),
            Flag(set, "repeatable"),
            Number(set, "timesCompleted"));
    }

    public static IReadOnlyList<SbcChallengeRecord> ParseChallenges(string json)
    {
        List<SbcChallengeRecord> result = [];

        try
        {
            using var document = JsonDocument.Parse(json);

            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("challenges", out var challenges) &&
                challenges.ValueKind == JsonValueKind.Array)
                result = challenges.EnumerateArray().Select(ReadChallenge).ToList();
        }
        catch (JsonException)
        {
        }

        return result;
    }

    private static SbcChallengeRecord ReadChallenge(JsonElement challenge)
    {
        return new SbcChallengeRecord(
            Number(challenge, "challengeId"),
            Number(challenge, "setId"),
            Text(challenge, "name"),
            Text(challenge, "description"),
            Text(challenge, "formation"),
            Text(challenge, "status"),
            Text(challenge, "type"),
            Text(challenge, "elgOperation"),
            Text(challenge, "challengeImageId"),
            Number(challenge, "contentId"),
            Number(challenge, "priority"),
            Identifier(challenge, "endTime"),
            Flag(challenge, "repeatable"),
            Number(challenge, "timesCompleted"),
            Number(challenge, "tutorial"),
            Raw(challenge, "elgReq"),
            Raw(challenge, "elgDesc"),
            Wrap(challenge));
    }

    private static string Wrap(JsonElement challenge)
    {
        return $"{{\"challenges\":[{challenge.GetRawText()}]}}";
    }

    private static string Raw(JsonElement element, string name)
    {
        var result = "[]";

        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value))
            result = value.GetRawText();

        return result;
    }

    private static string Text(JsonElement element, string name)
    {
        var result = string.Empty;

        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.String)
            result = value.GetString() ?? string.Empty;

        return result;
    }

    private static int Number(JsonElement element, string name)
    {
        return (int)Math.Clamp(Identifier(element, name), int.MinValue, int.MaxValue);
    }

    private static long Identifier(JsonElement element, string name)
    {
        var result = 0L;

        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var parsed))
            result = parsed;

        return result;
    }

    private static bool Flag(JsonElement element, string name)
    {
        var result = false;

        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value))
            result = value.ValueKind == JsonValueKind.True;

        return result;
    }
}
