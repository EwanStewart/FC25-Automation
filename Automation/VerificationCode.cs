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

    [GeneratedRegex(@"(?<!\d)(\d{6})(?!\d)")]
    private static partial Regex SixDigits();
}
