namespace Automation.Catalogue;

public sealed record PlayerRecord(
    long FutDbId,
    long? ResourceId,
    long? AssetId,
    string Name,
    string? CommonName,
    int? Rating,
    string? PreferredPosition,
    IReadOnlyList<string> AlternatePositions,
    long? ClubId,
    long? LeagueId,
    long? NationId,
    long? RarityId);

public sealed record CataloguePage(int PageCurrent, int PageTotal, int CountTotal, IReadOnlyList<PlayerRecord> Players);
