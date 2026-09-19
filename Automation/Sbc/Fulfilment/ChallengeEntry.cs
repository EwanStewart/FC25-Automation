namespace Automation.Sbc.Fulfilment;

public static class ChallengeEntry
{
    public const string START = "Start Challenge";
    public const string RESUME = "Go to Challenge";

    private static readonly string[] OPENING = [START, RESUME];

    public static string Opening(IReadOnlyList<string> labels)
    {
        var result = Wanted(labels);

        if (result.Length > 0) ForbiddenControls.Require(result);

        return result;
    }

    private static string Wanted(IReadOnlyList<string> labels)
    {
        return labels.Select(label => label.Trim())
            .FirstOrDefault(label => OPENING.Contains(label, StringComparer.OrdinalIgnoreCase)) ?? string.Empty;
    }
}
