namespace Automation.Catalogue;

public sealed record PlayerRecord(
    int DefinitionId,
    long? ResourceId,
    string Name,
    string? CommonName,
    int? Rating,
    string? PreferredPosition,
    IReadOnlyList<string> AlternatePositions,
    int? ClubId,
    int? LeagueId,
    int? NationId,
    int? RarityId,
    string? CardColour);

public sealed record CataloguePage(int PageCurrent, int PageTotal, int CountTotal, IReadOnlyList<PlayerRecord> Players);
