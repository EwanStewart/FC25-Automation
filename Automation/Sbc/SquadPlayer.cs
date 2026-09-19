namespace Automation.Sbc;

public sealed record SquadPlayer(
    long Id,
    int AssetId,
    string Name,
    int Rating,
    string PreferredPosition,
    IReadOnlyList<string> PossiblePositions,
    int TeamId,
    int LeagueId,
    int NationId,
    int RareFlag,
    bool Untradeable,
    int MarketAverage,
    bool Owned)
{
    public ChemistryPlayer ToChemistryPlayer()
    {
        return new ChemistryPlayer(TeamId, LeagueId, NationId, PossiblePositions, RareFlag);
    }
}

public static class QualityBand
{
    public const int SILVER_FLOOR = 65;
    public const int GOLD_FLOOR = 75;

    public static PlayerQuality Of(int rating)
    {
        return rating switch
        {
            >= GOLD_FLOOR => PlayerQuality.Gold,
            >= SILVER_FLOOR => PlayerQuality.Silver,
            _ => PlayerQuality.Bronze
        };
    }
}
