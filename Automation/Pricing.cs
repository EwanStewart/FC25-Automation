using System.Text.RegularExpressions;

namespace Automation.Trading;

public static class Pricing
{
    public const double TAX_RATE = 0.05;

    private static readonly Regex TimeRemainingPattern = new(@"^(<)?(\d+)\s+(Minute|Hour)s?$", RegexOptions.IgnoreCase);

    public static uint BidIncrement(uint price)
    {
        uint result = 1000;

        if (price < 1000) result = 50;
        else if (price < 10000) result = 100;
        else if (price < 50000) result = 250;
        else if (price < 100000) result = 500;

        return result;
    }

    public static uint RoundDownToIncrement(uint price)
    {
        var increment = BidIncrement(price);

        return price / increment * increment;
    }

    public static uint MaxBid(uint resalePrice, uint marginCoins)
    {
        var breakEven = resalePrice * (1 - TAX_RATE) - marginCoins;
        uint result = 0;

        if (breakEven > 0) result = RoundDownToIncrement((uint)Math.Floor(breakEven));

        return result;
    }

    public static uint ListingPrice(uint lowestBuyNow)
    {
        var increment = BidIncrement(lowestBuyNow);
        uint result = 0;

        if (lowestBuyNow > increment) result = lowestBuyNow - increment;

        return result;
    }

    public static bool IsProfitable(uint sellPrice, uint paidPrice)
    {
        return sellPrice * (1 - TAX_RATE) >= paidPrice;
    }

    public static uint? ParseMinutesRemaining(string text)
    {
        uint? result = null;
        var trimmed = text.Trim();
        var match = TimeRemainingPattern.Match(trimmed);

        if (trimmed.Equals("Expired", StringComparison.OrdinalIgnoreCase))
            result = 0;
        else if (match.Success)
            result = MinutesFromMatch(match);

        return result;
    }

    private static uint MinutesFromMatch(Match match)
    {
        var value = uint.Parse(match.Groups[2].Value);
        var isHours = match.Groups[3].Value.StartsWith("Hour", StringComparison.OrdinalIgnoreCase);
        var lessThan = match.Groups[1].Success;
        var minutes = isHours ? value * 60 : value;

        return lessThan ? minutes - 1 : minutes;
    }

    public static bool IsWithinBidWindow(uint minutesRemaining, uint minMinutes, uint maxMinutes)
    {
        return minutesRemaining >= minMinutes && minutesRemaining <= maxMinutes;
    }

    public static uint? EstimateResale(IEnumerable<uint> sightings, int sampleSize)
    {
        var lowest = sightings.OrderBy(price => price).Take(sampleSize).ToList();
        uint? result = null;

        if (lowest.Count > 0) result = Median(lowest);

        return result;
    }

    private static uint Median(IReadOnlyList<uint> sorted)
    {
        var middle = sorted.Count / 2;
        var isEven = sorted.Count % 2 == 0;

        return isEven ? (sorted[middle - 1] + sorted[middle]) / 2 : sorted[middle];
    }

    public static bool IsStale(DateTime latestSighting, DateTime now, uint maxAgeHours)
    {
        return now - latestSighting > TimeSpan.FromHours(maxAgeHours);
    }

    public static bool FitsExposureLimit(uint balance, uint committedCoins, uint bid, double maxShare)
    {
        return committedCoins + bid <= balance * maxShare;
    }
}
