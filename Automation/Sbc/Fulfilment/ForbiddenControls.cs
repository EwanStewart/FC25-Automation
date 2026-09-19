namespace Automation.Sbc.Fulfilment;

public static class ForbiddenControls
{
    public static readonly IReadOnlyList<string> WORDS =
    [
        "submit",
        "exchange",
        "complete sbc",
        "sell",
        "discard",
        "list on transfer market",
        "transfer list"
    ];

    public static void Require(string label)
    {
        var banned = WORDS.FirstOrDefault(word => label.Contains(word, StringComparison.OrdinalIgnoreCase));

        if (banned is not null)
            throw new InvalidOperationException(
                $"The control '{label}' matches the forbidden word '{banned}' and must never be clicked.");
    }
}
