using System.Text.Json;

namespace Automation.Sbc;

public sealed record EligibilityEntry(string Type, int Slot, int Key, int Value);

public static class RequirementParser
{
    public static IReadOnlyList<ChallengeRequirements> Parse(string json)
    {
        List<ChallengeRequirements> result = [];
        using var document = JsonDocument.Parse(json);

        if (document.RootElement.TryGetProperty("challenges", out var challenges) &&
            challenges.ValueKind == JsonValueKind.Array)
            result.AddRange(challenges.EnumerateArray().Select(ReadChallenge));

        return result;
    }

    private static ChallengeRequirements ReadChallenge(JsonElement challenge)
    {
        var entries = ReadEntries(challenge);

        return new ChallengeRequirements(
            ReadInt(challenge, "challengeId"),
            ReadInt(challenge, "setId"),
            ReadString(challenge, "name"),
            ReadString(challenge, "formation"),
            Build(entries));
    }

    private static IReadOnlyList<EligibilityEntry> ReadEntries(JsonElement challenge)
    {
        List<EligibilityEntry> result = [];

        if (challenge.TryGetProperty("elgReq", out var requirements) &&
            requirements.ValueKind == JsonValueKind.Array)
            result.AddRange(requirements.EnumerateArray().Select(entry => new EligibilityEntry(
                ReadString(entry, "type"),
                ReadInt(entry, "eligibilitySlot"),
                ReadInt(entry, "eligibilityKey"),
                ReadInt(entry, "eligibilityValue"))));

        return result;
    }

    public static IReadOnlyList<SquadRequirement> Build(IReadOnlyList<EligibilityEntry> entries)
    {
        return entries.GroupBy(entry => entry.Slot).OrderBy(group => group.Key).SelectMany(BuildSlot).ToList();
    }

    private static IReadOnlyList<SquadRequirement> BuildSlot(IEnumerable<EligibilityEntry> slot)
    {
        var entries = slot.ToList();
        var comparison = ReadComparison(entries);
        var count = ReadCount(entries);
        var filters = ReadFilters(entries);
        List<SquadRequirement> result = [];

        result.AddRange(entries.Where(IsSquadLevel)
            .Select(entry => SquadLevel(entry, comparison)));
        result.AddRange(entries.Where(IsUnsupported).Select(Unsupported));

        if (filters.Count > 0 && count.HasValue)
            result.Add(PlayerCount(comparison, count.Value, filters));
        else if (filters.Count > 0)
            result.Add(EveryPlayer(comparison, filters));
        else if (count.HasValue && result.Count == 0)
            result.Add(PlayerCount(comparison, count.Value, filters));

        return result;
    }

    private static RequirementComparison ReadComparison(IEnumerable<EligibilityEntry> entries)
    {
        var scope = entries.FirstOrDefault(entry => entry.Key == EligibilityKey.SCOPE);

        return scope is null ? RequirementComparison.Minimum : Comparison(scope.Value);
    }

    private static RequirementComparison Comparison(int scope)
    {
        return scope switch
        {
            EligibilityScope.LOWER => RequirementComparison.Maximum,
            EligibilityScope.EXACT => RequirementComparison.Exact,
            _ => RequirementComparison.Minimum
        };
    }

    private static int? ReadCount(IEnumerable<EligibilityEntry> entries)
    {
        var counted = entries.FirstOrDefault(entry =>
            entry.Key is EligibilityKey.PLAYER_COUNT or EligibilityKey.PLAYER_COUNT_COMBINED);

        return counted?.Value;
    }

    private static IReadOnlyList<PlayerFilter> ReadFilters(IEnumerable<EligibilityEntry> entries)
    {
        return entries.Where(entry => FilterKind(entry.Key).HasValue)
            .Select(entry => new PlayerFilter(FilterKind(entry.Key)!.Value, entry.Value,
                FilterLabel(FilterKind(entry.Key)!.Value, entry.Value))).ToList();
    }

    private static PlayerFilterKind? FilterKind(int key)
    {
        return key switch
        {
            EligibilityKey.PLAYER_QUALITY => PlayerFilterKind.Quality,
            EligibilityKey.PLAYER_RARITY => PlayerFilterKind.Rarity,
            EligibilityKey.PLAYER_RARITY_GROUP => PlayerFilterKind.RarityGroup,
            EligibilityKey.NATION_ID => PlayerFilterKind.Nation,
            EligibilityKey.LEAGUE_ID => PlayerFilterKind.League,
            EligibilityKey.CLUB_ID => PlayerFilterKind.Club,
            EligibilityKey.PLAYER_MIN_OVR => PlayerFilterKind.MinimumRating,
            EligibilityKey.PLAYER_MAX_OVR => PlayerFilterKind.MaximumRating,
            EligibilityKey.PLAYER_EXACT_OVR => PlayerFilterKind.ExactRating,
            EligibilityKey.PLAYER_TRADABILITY => PlayerFilterKind.Tradability,
            EligibilityKey.FIRST_OWNER_PLAYERS_COUNT => PlayerFilterKind.FirstOwner,
            _ => null
        };
    }

    public static string FilterLabel(PlayerFilterKind kind, int value)
    {
        return kind switch
        {
            PlayerFilterKind.Quality => QualityName(value),
            PlayerFilterKind.Rarity => $"Rarity {value}",
            PlayerFilterKind.RarityGroup => $"Rarity group {value}",
            PlayerFilterKind.Nation => $"Nation {value}",
            PlayerFilterKind.League => $"League {value}",
            PlayerFilterKind.Club => $"Club {value}",
            PlayerFilterKind.MinimumRating => $"Rating {value} or better",
            PlayerFilterKind.MaximumRating => $"Rating {value} or worse",
            PlayerFilterKind.ExactRating => $"Rating exactly {value}",
            PlayerFilterKind.Tradability => value == 0 ? "Untradeable" : "Tradeable",
            _ => $"First owner {value}"
        };
    }

    public static string QualityName(int quality)
    {
        return quality switch
        {
            (int)PlayerQuality.Bronze => "Bronze",
            (int)PlayerQuality.Silver => "Silver",
            (int)PlayerQuality.Gold => "Gold",
            _ => $"Quality {quality}"
        };
    }

    private static bool IsSquadLevel(EligibilityEntry entry)
    {
        return SquadLevelKind(entry.Key).HasValue;
    }

    private static RequirementKind? SquadLevelKind(int key)
    {
        return key switch
        {
            EligibilityKey.TEAM_RATING => RequirementKind.SquadRating,
            EligibilityKey.TEAM_STAR_RATING => RequirementKind.StarRating,
            EligibilityKey.ALL_PLAYERS_CHEMISTRY_POINTS => RequirementKind.TotalChemistry,
            EligibilityKey.CHEMISTRY_POINTS => RequirementKind.PlayerChemistry,
            EligibilityKey.NATION_COUNT => RequirementKind.DistinctNations,
            EligibilityKey.LEAGUE_COUNT => RequirementKind.DistinctLeagues,
            EligibilityKey.CLUB_COUNT => RequirementKind.DistinctClubs,
            EligibilityKey.SAME_NATION_COUNT => RequirementKind.SameNationCount,
            EligibilityKey.SAME_LEAGUE_COUNT => RequirementKind.SameLeagueCount,
            EligibilityKey.SAME_CLUB_COUNT => RequirementKind.SameClubCount,
            EligibilityKey.LEGEND_COUNT => RequirementKind.LegendCount,
            _ => null
        };
    }

    private static bool IsUnsupported(EligibilityEntry entry)
    {
        return !IsSquadLevel(entry) && !FilterKind(entry.Key).HasValue &&
               entry.Key is not (EligibilityKey.SCOPE or EligibilityKey.PLAYER_COUNT
                   or EligibilityKey.PLAYER_COUNT_COMBINED);
    }

    private static SquadRequirement Unsupported(EligibilityEntry entry)
    {
        return new SquadRequirement(RequirementKind.Unsupported, RequirementComparison.Minimum, entry.Value, [],
            $"Unsupported requirement {entry.Type} (eligibility key {entry.Key}, value {entry.Value})");
    }

    private static SquadRequirement SquadLevel(EligibilityEntry entry, RequirementComparison comparison)
    {
        var kind = SquadLevelKind(entry.Key)!.Value;
        var value = kind == RequirementKind.StarRating ? entry.Value / 2 : entry.Value;

        return new SquadRequirement(kind, comparison, value, [], Describe(kind, comparison, value, []));
    }

    private static SquadRequirement PlayerCount(RequirementComparison comparison, int count,
        IReadOnlyList<PlayerFilter> filters)
    {
        return new SquadRequirement(RequirementKind.PlayerCount, comparison, count, filters,
            Describe(RequirementKind.PlayerCount, comparison, count, filters));
    }

    private static SquadRequirement EveryPlayer(RequirementComparison comparison, IReadOnlyList<PlayerFilter> filters)
    {
        return new SquadRequirement(RequirementKind.EveryPlayer, comparison, 0, filters,
            Describe(RequirementKind.EveryPlayer, comparison, 0, filters));
    }

    public static string Describe(RequirementKind kind, RequirementComparison comparison, int value,
        IReadOnlyList<PlayerFilter> filters)
    {
        var word = ComparisonWord(comparison);
        var subject = string.Join(" and ", filters.Select(filter => filter.Label));

        return kind switch
        {
            RequirementKind.EveryPlayer => $"Player Quality: {word} {subject}".Replace("  ", " ").Trim(),
            RequirementKind.PlayerCount when filters.Count == 0 => $"Number of Players in the Squad: {value}",
            RequirementKind.PlayerCount => $"{subject}: {word} {value} Players",
            RequirementKind.SquadRating => $"Squad Rating: {word} {value}",
            RequirementKind.StarRating => $"Team Star Rating: {word} {value}",
            RequirementKind.TotalChemistry => $"Total Chemistry: {word} {value}",
            RequirementKind.PlayerChemistry => $"Chemistry Points per Player: {word} {value}",
            RequirementKind.DistinctNations => $"Nations in Squad: {word} {value}",
            RequirementKind.DistinctLeagues => $"Leagues in Squad: {word} {value}",
            RequirementKind.DistinctClubs => $"Clubs in Squad: {word} {value}",
            RequirementKind.SameNationCount => $"Same Nation Count: {word} {value}",
            RequirementKind.SameLeagueCount => $"Same League Count: {word} {value}",
            RequirementKind.SameClubCount => $"Same Club Count: {word} {value}",
            RequirementKind.LegendCount => $"Icons: {word} {value} Players",
            _ => $"Unsupported requirement, value {value}"
        };
    }

    private static string ComparisonWord(RequirementComparison comparison)
    {
        return comparison switch
        {
            RequirementComparison.Maximum => "Max.",
            RequirementComparison.Exact => "Exactly",
            _ => "Min."
        };
    }

    private static int ReadInt(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.TryGetInt32(out var number) ? number : 0;
    }

    private static string ReadString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }
}
