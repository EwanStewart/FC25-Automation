using System.Text.RegularExpressions;

namespace Automation.Sbc;

public sealed record NameLookup(
    IReadOnlyDictionary<string, int> Nations,
    IReadOnlyDictionary<string, int> Leagues,
    IReadOnlyDictionary<string, int> Clubs)
{
    public static NameLookup Empty { get; } = new(
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
}

public static class RequirementTextParser
{
    private static readonly Regex Value = new(@"^(Min\.|Max\.|Exactly)?\s*(-?\d+)", RegexOptions.IgnoreCase);

    private static readonly Regex Quality =
        new(@"^(Min\.|Max\.|Exactly)?\s*(Bronze|Silver|Gold)", RegexOptions.IgnoreCase);

    private static readonly IReadOnlyDictionary<string, RequirementKind> SquadLevel =
        new Dictionary<string, RequirementKind>(StringComparer.OrdinalIgnoreCase)
        {
            ["Total Chemistry"] = RequirementKind.TotalChemistry,
            ["Chemistry Points per Player"] = RequirementKind.PlayerChemistry,
            ["Squad Rating"] = RequirementKind.SquadRating,
            ["Team Star Rating"] = RequirementKind.StarRating,
            ["Clubs in Squad"] = RequirementKind.DistinctClubs,
            ["Leagues in Squad"] = RequirementKind.DistinctLeagues,
            ["Nations in Squad"] = RequirementKind.DistinctNations,
            ["Same Club Count"] = RequirementKind.SameClubCount,
            ["Same League Count"] = RequirementKind.SameLeagueCount,
            ["Same Nation Count"] = RequirementKind.SameNationCount,
            ["Icons"] = RequirementKind.LegendCount
        };

    private static readonly IReadOnlyDictionary<string, int> Rarities =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Common"] = 0, ["Rare"] = 1 };

    private const string SQUAD_SIZE_LABEL = "Number of Players in the Squad";
    private const string PLAYER_QUALITY_LABEL = "Player Quality";

    public static IReadOnlyList<SquadRequirement> Parse(IEnumerable<string> lines, NameLookup names)
    {
        return lines.Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => ParseLine(line, names)).ToList();
    }

    private static SquadRequirement ParseLine(string line, NameLookup names)
    {
        var separator = line.IndexOf(':');
        SquadRequirement result;

        if (separator < 0)
            result = Unsupported(line);
        else
            result = ParseParts(line, line[..separator].Trim(), line[(separator + 1)..].Trim(), names);

        return result;
    }

    private static SquadRequirement ParseParts(string line, string subject, string detail, NameLookup names)
    {
        SquadRequirement result;

        if (subject.Equals(SQUAD_SIZE_LABEL, StringComparison.OrdinalIgnoreCase))
            result = SquadSize(line, detail);
        else if (subject.Equals(PLAYER_QUALITY_LABEL, StringComparison.OrdinalIgnoreCase))
            result = EveryPlayerQuality(line, detail);
        else if (SquadLevel.TryGetValue(subject, out var kind))
            result = SquadLevelRequirement(line, kind, detail);
        else
            result = FilteredCount(line, subject, detail, names);

        return result;
    }

    private static SquadRequirement SquadSize(string line, string detail)
    {
        var match = Value.Match(detail);

        return match.Success
            ? new SquadRequirement(RequirementKind.PlayerCount, RequirementComparison.Exact,
                int.Parse(match.Groups[2].Value), [], line)
            : Unsupported(line);
    }

    private static SquadRequirement EveryPlayerQuality(string line, string detail)
    {
        var match = Quality.Match(detail);
        SquadRequirement result;

        if (match.Success)
        {
            var quality = QualityValue(match.Groups[2].Value);
            PlayerFilter[] filters = [new(PlayerFilterKind.Quality, quality, RequirementParser.QualityName(quality))];
            result = new SquadRequirement(RequirementKind.EveryPlayer, Comparison(match.Groups[1].Value), 0, filters,
                line);
        }
        else
        {
            result = Unsupported(line);
        }

        return result;
    }

    private static SquadRequirement SquadLevelRequirement(string line, RequirementKind kind, string detail)
    {
        var match = Value.Match(detail);

        return match.Success
            ? new SquadRequirement(kind, Comparison(match.Groups[1].Value), int.Parse(match.Groups[2].Value), [], line)
            : Unsupported(line);
    }

    private static SquadRequirement FilteredCount(string line, string subject, string detail, NameLookup names)
    {
        var match = Value.Match(detail);
        var filter = ResolveFilter(subject, names);
        SquadRequirement result;

        if (match.Success && filter is not null)
            result = new SquadRequirement(RequirementKind.PlayerCount, Comparison(match.Groups[1].Value),
                int.Parse(match.Groups[2].Value), [filter], line);
        else
            result = Unsupported(line);

        return result;
    }

    private static PlayerFilter? ResolveFilter(string subject, NameLookup names)
    {
        PlayerFilter? result = null;

        if (Quality.IsMatch(subject))
            result = new PlayerFilter(PlayerFilterKind.Quality, QualityValue(subject), subject);
        else if (Rarities.TryGetValue(subject, out var rarity))
            result = new PlayerFilter(PlayerFilterKind.Rarity, rarity, subject);
        else if (names.Nations.TryGetValue(subject, out var nation))
            result = new PlayerFilter(PlayerFilterKind.Nation, nation, subject);
        else if (names.Leagues.TryGetValue(subject, out var league))
            result = new PlayerFilter(PlayerFilterKind.League, league, subject);
        else if (names.Clubs.TryGetValue(subject, out var club))
            result = new PlayerFilter(PlayerFilterKind.Club, club, subject);

        return result;
    }

    private static int QualityValue(string name)
    {
        var trimmed = name.Trim();

        return trimmed switch
        {
            _ when trimmed.StartsWith("Bronze", StringComparison.OrdinalIgnoreCase) => (int)PlayerQuality.Bronze,
            _ when trimmed.StartsWith("Silver", StringComparison.OrdinalIgnoreCase) => (int)PlayerQuality.Silver,
            _ => (int)PlayerQuality.Gold
        };
    }

    private static RequirementComparison Comparison(string word)
    {
        return word.Trim().ToUpperInvariant() switch
        {
            "MAX." => RequirementComparison.Maximum,
            "EXACTLY" => RequirementComparison.Exact,
            _ => RequirementComparison.Minimum
        };
    }

    private static SquadRequirement Unsupported(string line)
    {
        return new SquadRequirement(RequirementKind.Unsupported, RequirementComparison.Minimum, 0, [],
            $"Unsupported requirement text: {line}");
    }
}
