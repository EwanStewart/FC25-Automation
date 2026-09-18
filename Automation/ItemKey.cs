namespace Automation.Flow;

public static class ItemKey
{
    private const string BADGE_CLASS = "badge";
    private const string PLAYER_CLASS = "player";

    public static string Build(string name, string itemDescription, string itemClasses, string rating, string position)
    {
        var description = itemDescription.Trim();
        var classes = itemClasses.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

        if (description.Length == 0 && classes.Contains(BADGE_CLASS)) description = "Badge";
        else if (description.Length == 0 && classes.Contains(PLAYER_CLASS)) description = $"{rating.Trim()} {position.Trim()}".Trim();

        return $"{name.Trim()} {description}".Trim();
    }
}

public readonly record struct BidContext(
    uint MinimumBid,
    uint? CurrentBid,
    uint? BuyNow,
    uint? MinutesLeft,
    int AskCount);
