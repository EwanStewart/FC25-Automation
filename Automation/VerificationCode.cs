using System.Text.Json;
using System.Text.RegularExpressions;

namespace Automation.Flow;

public static partial class VerificationCode
{
    private const string SECURITY_CODE_MARKER = "security code";

    public static string FromSubject(string subject)
    {
        var result = string.Empty;
        var carriesMarker = subject.Contains(SECURITY_CODE_MARKER, StringComparison.OrdinalIgnoreCase);
        var match = carriesMarker ? SixDigits().Match(subject) : Match.Empty;

        if (match.Success) result = match.Groups[1].Value;

        return result;
    }

    public static string FromFeed(IEnumerable<string> feed, DateTimeOffset requestedAt)
    {
        var result = string.Empty;
        var cutoff = requestedAt.ToUnixTimeMilliseconds();

        foreach (var line in feed)
        {
            var entry = ReadEntry(line);

            if (entry.Received >= cutoff && result.Length == 0) result = FromSubject(entry.Subject);
        }

        return result;
    }

    private static (long Received, string Subject) ReadEntry(string line)
    {
        var result = (Received: 0L, Subject: string.Empty);

        try
        {
            using var document = JsonDocument.Parse(line);
            result = (document.RootElement.GetProperty("internalDate").GetInt64(),
                document.RootElement.GetProperty("subject").GetString() ?? string.Empty);
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException
                                              or InvalidOperationException)
        {
            result = (0L, string.Empty);
        }

        return result;
    }

    [GeneratedRegex(@"(?<!\d)(\d{6})(?!\d)")]
    private static partial Regex SixDigits();
}
