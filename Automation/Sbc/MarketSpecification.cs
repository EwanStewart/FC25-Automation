namespace Automation.Sbc;

public sealed record MarketSpecification(
    string Position,
    PlayerQuality Quality,
    int MinimumRating,
    int MaximumRating,
    int? NationId,
    int? LeagueId,
    int? ClubId,
    int? RareFlag,
    int EstimatedCost,
    MarketEvidence Evidence = MarketEvidence.Unverified)
{
    public string Describe()
    {
        List<string> parts = [$"{Quality} {Position}", $"rating {MinimumRating} to {MaximumRating}"];

        if (NationId.HasValue) parts.Add($"nation {NationId}");
        if (LeagueId.HasValue) parts.Add($"league {LeagueId}");
        if (ClubId.HasValue) parts.Add($"club {ClubId}");
        if (RareFlag.HasValue) parts.Add($"rarity {RareFlag}");
        parts.Add(Evidence == MarketEvidence.Observed ? "observed" : "unverified");

        return string.Join(", ", parts);
    }
}

public static class MarketPricing
{
    private const int BRONZE_BASE = 200;
    private const int SILVER_BASE = 600;
    private const int GOLD_BASE = 1200;
    private const int BRONZE_STEP = 20;
    private const int SILVER_STEP = 120;
    private const int GOLD_STEP = 400;
    private const double CLUB_PREMIUM = 1.5;
    private const double NAMED_PREMIUM = 1.2;
    private const double RARITY_PREMIUM = 1.3;
    private const double FOREIGN_PREMIUM = 1.15;

    public static int Estimate(PlayerQuality quality, int rating, bool pinnedClub, bool pinnedNationOrLeague,
        bool pinnedRarity, bool domestic = true)
    {
        var floor = Floor(quality);
        var basePrice = Base(quality) + Math.Max(0, rating - floor) * Step(quality);
        var multiplier = 1.0;

        if (pinnedClub) multiplier *= CLUB_PREMIUM;
        if (pinnedNationOrLeague) multiplier *= NAMED_PREMIUM;
        if (pinnedRarity) multiplier *= RARITY_PREMIUM;
        if (!domestic) multiplier *= FOREIGN_PREMIUM;

        return (int)Math.Round(basePrice * multiplier);
    }

    public static int Floor(PlayerQuality quality)
    {
        return quality switch
        {
            PlayerQuality.Gold => QualityBand.GOLD_FLOOR,
            PlayerQuality.Silver => QualityBand.SILVER_FLOOR,
            _ => 45
        };
    }

    public static int Ceiling(PlayerQuality quality)
    {
        return quality switch
        {
            PlayerQuality.Gold => 91,
            PlayerQuality.Silver => QualityBand.GOLD_FLOOR - 1,
            _ => QualityBand.SILVER_FLOOR - 1
        };
    }

    private static int Base(PlayerQuality quality)
    {
        return quality switch
        {
            PlayerQuality.Gold => GOLD_BASE,
            PlayerQuality.Silver => SILVER_BASE,
            _ => BRONZE_BASE
        };
    }

    private static int Step(PlayerQuality quality)
    {
        return quality switch
        {
            PlayerQuality.Gold => GOLD_STEP,
            PlayerQuality.Silver => SILVER_STEP,
            _ => BRONZE_STEP
        };
    }
}
