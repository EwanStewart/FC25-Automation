namespace Automation.Sbc;

public enum RequirementComparison
{
    Minimum,
    Maximum,
    Exact
}

public enum PlayerQuality
{
    Bronze = 1,
    Silver = 2,
    Gold = 3
}

public enum RequirementKind
{
    PlayerCount,
    PlayerLevelCount,
    EveryPlayer,
    SquadRating,
    StarRating,
    TotalChemistry,
    PlayerChemistry,
    DistinctNations,
    DistinctLeagues,
    DistinctClubs,
    SameNationCount,
    SameLeagueCount,
    SameClubCount,
    LegendCount,
    Unsupported
}

public enum PlayerFilterKind
{
    Quality,
    Level,
    Rarity,
    Nation,
    League,
    Club,
    MinimumRating,
    MaximumRating,
    ExactRating,
    Tradability
}

public sealed record PlayerFilter(
    PlayerFilterKind Kind,
    int Value,
    string Label,
    IReadOnlyList<int>? Alternatives = null)
{
    public bool Accepts(int value)
    {
        return value == Value || (Alternatives?.Contains(value) ?? false);
    }
}

public sealed record SquadRequirement(
    RequirementKind Kind,
    RequirementComparison Comparison,
    int Value,
    IReadOnlyList<PlayerFilter> Filters,
    string Description,
    string Subject = "");

public sealed record ChallengeRequirements(
    int ChallengeId,
    int SetId,
    string Name,
    string Formation,
    IReadOnlyList<SquadRequirement> Requirements)
{
    public bool IsFullyUnderstood => Requirements.All(requirement => requirement.Kind != RequirementKind.Unsupported);

    public int SquadSize
    {
        get
        {
            var sized = Requirements.FirstOrDefault(requirement =>
                requirement.Kind == RequirementKind.PlayerCount && requirement.Filters.Count == 0);

            return sized?.Value ?? Formations.DEFAULT_SQUAD_SIZE;
        }
    }
}
