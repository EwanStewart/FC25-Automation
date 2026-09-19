using System.Reflection;
using System.Text.Json;

namespace Automation.Sbc;

public sealed record LeagueName(string Name, string Abbreviation);

public sealed class MarketNames
{
    private const string RESOURCE_NAME = "Automation.Sbc.MarketNamesData.json";

    private static readonly Lazy<MarketNames> SHIPPED = new(LoadShipped);

    private readonly IReadOnlyDictionary<int, string> nations_;
    private readonly IReadOnlyDictionary<int, LeagueName> leagues_;
    private readonly IReadOnlyDictionary<int, string> clubs_;
    private readonly IReadOnlySet<string> sharedClubNames_;

    public MarketNames(string year, IReadOnlyDictionary<int, string> nations,
        IReadOnlyDictionary<int, LeagueName> leagues, IReadOnlyDictionary<int, string> clubs)
    {
        Year = year;
        nations_ = nations;
        leagues_ = leagues;
        clubs_ = clubs;
        sharedClubNames_ = clubs.Values.GroupBy(name => name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet(StringComparer.Ordinal);
    }

    public static MarketNames Ea => SHIPPED.Value;

    public string Year { get; }

    public string? Nation(int nationId)
    {
        return nations_.TryGetValue(nationId, out var name) ? name : null;
    }

    public string? League(int leagueId)
    {
        return leagues_.TryGetValue(leagueId, out var league)
            ? $"{league.Name} ({league.Abbreviation})"
            : null;
    }

    public string? Club(int clubId)
    {
        return clubs_.TryGetValue(clubId, out var name) ? name : null;
    }

    public bool ClubNameIsUnique(int clubId)
    {
        var name = Club(clubId);

        return name is not null && !sharedClubNames_.Contains(name);
    }

    private static MarketNames LoadShipped()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(RESOURCE_NAME);

        if (stream is null)
            throw new InvalidOperationException($"The assembly carries no {RESOURCE_NAME} name data.");

        return Parse(stream);
    }

    private static MarketNames Parse(Stream stream)
    {
        var data = JsonSerializer.Deserialize<NameData>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (data is null) throw new InvalidOperationException($"The {RESOURCE_NAME} name data is empty.");

        return new MarketNames(data.Year, data.Nations, data.Leagues, data.Clubs);
    }

    private sealed record NameData(
        string Year,
        Dictionary<int, string> Nations,
        Dictionary<int, LeagueName> Leagues,
        Dictionary<int, string> Clubs);
}
