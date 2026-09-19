namespace Automation.Sbc;

public sealed record ChemistryPlayer(
    int TeamId,
    int LeagueId,
    int NationId,
    IReadOnlyList<string> PossiblePositions,
    int RareFlag);

public sealed record ChemistryThreshold(int Requirement, int Points);

public sealed record ChemistryThresholds(
    IReadOnlyList<ChemistryThreshold> Club,
    IReadOnlyList<ChemistryThreshold> League,
    IReadOnlyList<ChemistryThreshold> Nation)
{
    public static ChemistryThresholds Default { get; } = new(
        [new ChemistryThreshold(2, 1), new ChemistryThreshold(4, 1), new ChemistryThreshold(7, 1)],
        [new ChemistryThreshold(3, 1), new ChemistryThreshold(5, 1), new ChemistryThreshold(8, 1)],
        [new ChemistryThreshold(2, 1), new ChemistryThreshold(5, 1), new ChemistryThreshold(8, 1)]);
}

public sealed record TeamLinks(IReadOnlyDictionary<int, int> Links)
{
    public static TeamLinks None { get; } = new(new Dictionary<int, int>());

    public int Resolve(int teamId)
    {
        return Links.TryGetValue(teamId, out var linked) ? linked : teamId;
    }
}

public sealed record ChemistryResult(int Total, IReadOnlyList<int> SlotPoints);

public static class ChemistryCalculator
{
    public const int SLOT_MAX_CHEMISTRY = 3;
    public const int FIELD_PLAYERS = 11;
    public const int LEGENDS_LEAGUE_ID = 2118;
    public const int LEGENDS_CLUB_ID = 112658;
    public const int LEAGUE_HERO_CLUB_ID = 114605;
    public const int HALL_OF_FUT_CLUB_ID = 132794;

    private sealed class Bucket
    {
        public int Contributions { get; set; }
        public bool UniversalAdded { get; set; }
    }

    public static ChemistryResult Calculate(IReadOnlyList<string> slotPositions,
        IReadOnlyList<ChemistryPlayer?> players, ChemistryPlayer? manager, ChemistryThresholds thresholds,
        TeamLinks links)
    {
        var field = players.Take(FIELD_PLAYERS).ToList();
        var placements = field
            .Select((player, index) => new Placement(player, InPosition(slotPositions, index, player), false))
            .ToList();
        var clubs = new Dictionary<int, Bucket>();
        var leagues = new Dictionary<int, Bucket>();
        var nations = new Dictionary<int, Bucket>();

        Gather(placements, manager, links, clubs, leagues, nations);
        var slotPoints = placements
            .Select(placement => Score(placement, thresholds, links, clubs, leagues, nations)).ToList();

        return new ChemistryResult(slotPoints.Sum(), slotPoints);
    }

    private sealed record Placement(ChemistryPlayer? Player, bool InPosition, bool IsManager);

    private static void Gather(IReadOnlyList<Placement> placements, ChemistryPlayer? manager, TeamLinks links,
        Dictionary<int, Bucket> clubs, Dictionary<int, Bucket> leagues, Dictionary<int, Bucket> nations)
    {
        var legendsInPosition = placements.Count(placement =>
            placement.Player is not null && IsLegend(placement.Player) && placement.InPosition);
        var legends = placements.Count(placement => placement.Player is not null && IsLegend(placement.Player));

        foreach (var placement in placements.Concat([new Placement(manager, true, true)]))
            GatherOne(placement, placements, links, clubs, leagues, nations, legendsInPosition, legends - legendsInPosition);
    }

    private static void GatherOne(Placement placement, IReadOnlyList<Placement> placements, TeamLinks links,
        Dictionary<int, Bucket> clubs, Dictionary<int, Bucket> leagues, Dictionary<int, Bucket> nations,
        int legendsInPosition, int legendsOutOfPosition)
    {
        var player = placement.Player;

        if (player is not null)
        {
            if (!IsRestrictedClub(player.TeamId))
                Add(clubs, links.Resolve(player.TeamId), ClubContribution(player, placement.IsManager),
                    placement.InPosition);

            if (!IsRestrictedLeague(player.LeagueId))
                AddLeague(leagues, player.LeagueId, LeagueContribution(player), placement.InPosition,
                    legendsInPosition, legendsOutOfPosition, SharesLeagueInPosition(placements, player.LeagueId));

            Add(nations, player.NationId, NationContribution(player), placement.InPosition);
        }
    }

    private static bool SharesLeagueInPosition(IReadOnlyList<Placement> placements, int leagueId)
    {
        return placements.Any(placement =>
            placement.Player is not null && placement.Player.LeagueId == leagueId && placement.InPosition);
    }

    private static void Add(Dictionary<int, Bucket> buckets, int metaId, int contribution, bool inPosition)
    {
        if (metaId > 0)
        {
            var bucket = Ensure(buckets, metaId);

            if (inPosition) bucket.Contributions += contribution;
        }
    }

    private static void AddLeague(Dictionary<int, Bucket> buckets, int metaId, int contribution, bool inPosition,
        int legendsInPosition, int legendsOutOfPosition, bool sharesLeague)
    {
        if (metaId > 0)
        {
            var seen = buckets.TryGetValue(metaId, out var existing) && existing.UniversalAdded;
            var bucket = Ensure(buckets, metaId);
            bucket.UniversalAdded = true;

            if (inPosition) bucket.Contributions += contribution;
            if (!seen && sharesLeague) bucket.Contributions += legendsInPosition;
        }
    }

    private static Bucket Ensure(Dictionary<int, Bucket> buckets, int metaId)
    {
        if (!buckets.TryGetValue(metaId, out var bucket))
        {
            bucket = new Bucket();
            buckets[metaId] = bucket;
        }

        return bucket;
    }

    private static int Score(Placement placement, ChemistryThresholds thresholds, TeamLinks links,
        Dictionary<int, Bucket> clubs, Dictionary<int, Bucket> leagues, Dictionary<int, Bucket> nations)
    {
        var player = placement.Player;
        var result = 0;

        if (player is not null && placement.InPosition)
        {
            result = IsMaxChemistry(player) ? SLOT_MAX_CHEMISTRY : 0;
            result += Points(clubs, links.Resolve(player.TeamId), thresholds.Club);
            result += Points(leagues, player.LeagueId, thresholds.League);
            result += Points(nations, player.NationId, thresholds.Nation);
            result = Math.Min(result, SLOT_MAX_CHEMISTRY);
        }

        return result;
    }

    private static int Points(Dictionary<int, Bucket> buckets, int metaId,
        IReadOnlyList<ChemistryThreshold> thresholds)
    {
        var contributions = buckets.TryGetValue(metaId, out var bucket) ? bucket.Contributions : 0;

        return contributions <= 0
            ? 0
            : thresholds.Where(threshold => contributions >= threshold.Requirement).Sum(threshold => threshold.Points);
    }

    private static bool InPosition(IReadOnlyList<string> slotPositions, int index, ChemistryPlayer? player)
    {
        return player is not null && index < slotPositions.Count &&
               player.PossiblePositions.Contains(slotPositions[index], StringComparer.OrdinalIgnoreCase);
    }

    private static int ClubContribution(ChemistryPlayer player, bool isManager)
    {
        return isManager || IsLeagueHero(player) || IsLegend(player) ? 0 : 1;
    }

    private static int LeagueContribution(ChemistryPlayer player)
    {
        return IsLeagueHero(player) ? 2 : 1;
    }

    private static int NationContribution(ChemistryPlayer player)
    {
        return IsLegend(player) ? 2 : 1;
    }

    private static bool IsMaxChemistry(ChemistryPlayer player)
    {
        return IsLegend(player) || IsLeagueHero(player);
    }

    private static bool IsLegend(ChemistryPlayer player)
    {
        return player.TeamId == LEGENDS_CLUB_ID || player.LeagueId == LEGENDS_LEAGUE_ID;
    }

    private static bool IsLeagueHero(ChemistryPlayer player)
    {
        return player.TeamId == LEAGUE_HERO_CLUB_ID || player.TeamId == HALL_OF_FUT_CLUB_ID;
    }

    private static bool IsRestrictedClub(int teamId)
    {
        return teamId is LEGENDS_CLUB_ID or LEAGUE_HERO_CLUB_ID or HALL_OF_FUT_CLUB_ID;
    }

    private static bool IsRestrictedLeague(int leagueId)
    {
        return leagueId == LEGENDS_LEAGUE_ID;
    }
}
