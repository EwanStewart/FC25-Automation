using System.Reflection;
using System.Text.Json;

namespace Automation.Sbc;

public sealed class MarketReference
{
    public const int UNKNOWN_NATION = 0;

    private const string RESOURCE_NAME = "Automation.Sbc.MarketReferenceData.json";

    private static readonly Lazy<MarketReference> SHIPPED = new(LoadShipped);

    private readonly IReadOnlyDictionary<int, int> clubLeagues_;
    private readonly IReadOnlyDictionary<int, int> leagueNations_;
    private readonly IReadOnlyDictionary<int, IReadOnlyList<int>> leagueClubs_;
    private readonly IReadOnlySet<int> nations_;

    public MarketReference(IReadOnlyList<int[]> clubs, IReadOnlyList<int[]> leagues, IReadOnlyList<int> nations)
    {
        nations_ = nations.Where(nation => nation > 0).ToHashSet();
        clubLeagues_ = clubs.Where(pair => Buyable(pair[0], pair[1]))
            .ToDictionary(pair => pair[0], pair => pair[1]);
        leagueClubs_ = clubLeagues_.GroupBy(entry => entry.Value).ToDictionary(group => group.Key,
            group => (IReadOnlyList<int>)group.Select(entry => entry.Key).Order().ToList());
        leagueNations_ = leagues.Where(pair => nations_.Contains(pair[1]))
            .ToDictionary(pair => pair[0], pair => pair[1]);
        Leagues = leagueClubs_.Keys.Order().ToList();
        LeaguesByClubCount = leagueClubs_.OrderByDescending(entry => entry.Value.Count).ThenBy(entry => entry.Key)
            .Select(entry => entry.Key).ToList();
        Clubs = clubLeagues_.Keys.Order().ToList();
        Nations = nations_.Order().ToList();
    }

    public static MarketReference Ea => SHIPPED.Value;

    public IReadOnlyList<int> Clubs { get; }

    public IReadOnlyList<int> Leagues { get; }

    public IReadOnlyList<int> LeaguesByClubCount { get; }

    public IReadOnlyList<int> Nations { get; }

    public bool KnowsClub(int clubId)
    {
        return clubLeagues_.ContainsKey(clubId);
    }

    public bool KnowsNation(int nationId)
    {
        return nations_.Contains(nationId);
    }

    public int LeagueOf(int clubId)
    {
        return clubLeagues_.TryGetValue(clubId, out var league) ? league : 0;
    }

    public IReadOnlyList<int> ClubsIn(int leagueId)
    {
        return leagueClubs_.TryGetValue(leagueId, out var clubs) ? clubs : [];
    }

    public int HomeNationOf(int leagueId)
    {
        return leagueNations_.TryGetValue(leagueId, out var nation) ? nation : UNKNOWN_NATION;
    }

    private static bool Buyable(int clubId, int leagueId)
    {
        return clubId > 0 && leagueId > 0 && leagueId != ChemistryCalculator.LEGENDS_LEAGUE_ID &&
               clubId is not (ChemistryCalculator.LEGENDS_CLUB_ID or ChemistryCalculator.LEAGUE_HERO_CLUB_ID
                   or ChemistryCalculator.HALL_OF_FUT_CLUB_ID);
    }

    private static MarketReference LoadShipped()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(RESOURCE_NAME);

        if (stream is null)
            throw new InvalidOperationException($"The assembly carries no {RESOURCE_NAME} reference data.");

        return Parse(stream);
    }

    private static MarketReference Parse(Stream stream)
    {
        var data = JsonSerializer.Deserialize<ReferenceData>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (data is null) throw new InvalidOperationException($"The {RESOURCE_NAME} reference data is empty.");

        return new MarketReference(data.Clubs, data.Leagues, data.Nations);
    }

    private sealed record ReferenceData(int[][] Clubs, int[][] Leagues, int[] Nations);
}
