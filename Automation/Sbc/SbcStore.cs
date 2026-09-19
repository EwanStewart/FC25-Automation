using Automation.Sbc.Fulfilment;
using MySql.Data.MySqlClient;

namespace Automation.Sbc;

public sealed record ApprovalRecord(
    int Id,
    int ChallengeId,
    string ChallengeName,
    string Formation,
    int SquadRating,
    int Chemistry,
    int PurchaseCount,
    long EstimatedCost,
    string State,
    DateTime ApprovedAt);

public sealed class SbcStore
{
    private readonly string connectionString_;

    public SbcStore()
        : this(Database.Connection)
    {
    }

    public SbcStore(string connectionString)
    {
        connectionString_ = connectionString;
    }

    public IReadOnlyList<SquadPlayer> ReadClubPlayers()
    {
        const string query =
            "SELECT c.id, c.asset_id, COALESCE(NULLIF(p.common_name, ''), p.name, CONCAT('Asset ', c.asset_id)), c.rating, c.preferred_position, c.possible_positions, c.team_id, c.league_id, c.nation, c.rare_flag, c.untradeable, c.market_average FROM ClubPlayers c LEFT JOIN Players p ON p.asset_id = c.asset_id WHERE c.id NOT IN (SELECT club_player_id FROM ExcludedClubPlayers) ORDER BY c.rating DESC, c.id";

        return Read(query, [], reader => new SquadPlayer(reader.GetInt64(0), reader.GetInt32(1), reader.GetString(2),
            reader.GetInt32(3), reader.GetString(4), Positions(reader.GetString(5)), reader.GetInt32(6),
            reader.GetInt32(7), reader.GetInt32(8), reader.GetInt32(9), reader.GetBoolean(10), reader.GetInt32(11),
            true));
    }

    private static IReadOnlyList<string> Positions(string csv)
    {
        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public void SaveCatalogue(SbcCatalogueReading reading)
    {
        foreach (var set in reading.Sets) SaveSet(set);

        foreach (var challenge in reading.Challenges) SaveChallenge(challenge);
    }

    private void SaveSet(SbcSet set)
    {
        const string query =
            "INSERT INTO SbcSets (set_id, name, description, category_id, category_name, priority, end_time, challenges_count, challenges_completed_count, repeatable, times_completed, completed) VALUES (@set, @name, @description, @category, @categoryName, @priority, @endTime, @count, @completedCount, @repeatable, @timesCompleted, @completed) AS incoming ON DUPLICATE KEY UPDATE name = incoming.name, description = incoming.description, category_id = incoming.category_id, category_name = incoming.category_name, priority = incoming.priority, end_time = incoming.end_time, challenges_count = incoming.challenges_count, challenges_completed_count = incoming.challenges_completed_count, repeatable = incoming.repeatable, times_completed = incoming.times_completed, completed = incoming.completed, captured_at = CURRENT_TIMESTAMP";

        Execute(query, new Dictionary<string, object>
        {
            ["@set"] = set.SetId,
            ["@name"] = set.Name,
            ["@description"] = set.Description,
            ["@category"] = set.CategoryId,
            ["@categoryName"] = set.CategoryName,
            ["@priority"] = set.Priority,
            ["@endTime"] = set.EndTime,
            ["@count"] = set.ChallengesCount,
            ["@completedCount"] = set.ChallengesCompletedCount,
            ["@repeatable"] = set.Repeatable,
            ["@timesCompleted"] = set.TimesCompleted,
            ["@completed"] = set.Completed
        });
    }

    private void SaveChallenge(SbcChallengeRecord challenge)
    {
        const string query =
            "INSERT INTO SbcChallenges (challenge_id, set_id, name, description, formation, status, challenge_type, eligibility_operation, challenge_image_id, content_id, priority, end_time, repeatable, times_completed, tutorial, eligibility, eligibility_description, requirements) VALUES (@challenge, @set, @name, @description, @formation, @status, @type, @operation, @image, @content, @priority, @endTime, @repeatable, @timesCompleted, @tutorial, @eligibility, @eligibilityDescription, @requirements) AS incoming ON DUPLICATE KEY UPDATE set_id = incoming.set_id, name = incoming.name, description = incoming.description, formation = incoming.formation, status = incoming.status, challenge_type = incoming.challenge_type, eligibility_operation = incoming.eligibility_operation, challenge_image_id = incoming.challenge_image_id, content_id = incoming.content_id, priority = incoming.priority, end_time = incoming.end_time, repeatable = incoming.repeatable, times_completed = incoming.times_completed, tutorial = incoming.tutorial, eligibility = incoming.eligibility, eligibility_description = incoming.eligibility_description, requirements = incoming.requirements, captured_at = CURRENT_TIMESTAMP";

        Execute(query, new Dictionary<string, object>
        {
            ["@challenge"] = challenge.ChallengeId,
            ["@set"] = challenge.SetId,
            ["@name"] = challenge.Name,
            ["@description"] = challenge.Description,
            ["@formation"] = challenge.Formation,
            ["@status"] = challenge.Status,
            ["@type"] = challenge.Type,
            ["@operation"] = challenge.ElgOperation,
            ["@image"] = challenge.ChallengeImageId,
            ["@content"] = challenge.ContentId,
            ["@priority"] = challenge.Priority,
            ["@endTime"] = challenge.EndTime,
            ["@repeatable"] = challenge.Repeatable,
            ["@timesCompleted"] = challenge.TimesCompleted,
            ["@tutorial"] = challenge.Tutorial,
            ["@eligibility"] = challenge.Eligibility,
            ["@eligibilityDescription"] = challenge.EligibilityDescription,
            ["@requirements"] = challenge.Body
        });
    }

    public IReadOnlyList<SbcSet> ReadSets()
    {
        const string query =
            "SELECT set_id, name, description, category_id, category_name, priority, end_time, challenges_count, challenges_completed_count, repeatable, times_completed FROM SbcSets ORDER BY category_id, set_id";

        return Read(query, [], reader => new SbcSet(reader.GetInt32(0), reader.GetString(1), reader.GetString(2),
            reader.GetInt32(3), reader.GetString(4), reader.GetInt32(5), reader.GetInt64(6), reader.GetInt32(7),
            reader.GetInt32(8), reader.GetBoolean(9), reader.GetInt32(10)));
    }

    public void SaveChallenges(IEnumerable<ChallengeRequirements> challenges, string body)
    {
        const string query =
            "INSERT INTO SbcChallenges (challenge_id, set_id, name, formation, requirements) VALUES (@challenge, @set, @name, @formation, @requirements) AS incoming ON DUPLICATE KEY UPDATE set_id = incoming.set_id, name = incoming.name, formation = incoming.formation, requirements = incoming.requirements, captured_at = CURRENT_TIMESTAMP";

        foreach (var challenge in challenges)
            Execute(query, new Dictionary<string, object>
            {
                ["@challenge"] = challenge.ChallengeId,
                ["@set"] = challenge.SetId,
                ["@name"] = challenge.Name,
                ["@formation"] = challenge.Formation,
                ["@requirements"] = body
            });
    }

    public IReadOnlyList<ChallengeRequirements> ReadChallenges()
    {
        const string query = "SELECT challenge_id, requirements FROM SbcChallenges ORDER BY set_id, challenge_id";
        var rows = Read(query, [], reader => (reader.GetInt32(0), reader.GetString(1)));

        return rows.SelectMany(row => RequirementParser.Parse(row.Item2))
            .GroupBy(challenge => challenge.ChallengeId).Select(group => group.First()).ToList();
    }

    public int SaveApproval(ChallengeRequirements challenge, SolvedSquad squad)
    {
        const string query =
            "INSERT INTO SbcApprovals (challenge_id, challenge_name, formation, squad_rating, chemistry, purchase_count, estimated_cost) VALUES (@challenge, @name, @formation, @rating, @chemistry, @purchases, @cost); SELECT LAST_INSERT_ID();";
        var id = Scalar(query, new Dictionary<string, object>
        {
            ["@challenge"] = challenge.ChallengeId,
            ["@name"] = challenge.Name,
            ["@formation"] = challenge.Formation,
            ["@rating"] = squad.Assessment?.Rating ?? 0,
            ["@chemistry"] = squad.Assessment?.Chemistry.Total ?? 0,
            ["@purchases"] = squad.PurchaseCount,
            ["@cost"] = squad.EstimatedCost
        });

        SaveSlots(id, squad);

        return id;
    }

    private void SaveSlots(int approvalId, SolvedSquad squad)
    {
        const string query =
            "INSERT INTO SbcApprovalSlots (approval_id, slot_index, position, club_player_id, player_name, rating, owned, chemistry, specification, estimated_cost) VALUES (@approval, @index, @position, @club, @name, @rating, @owned, @chemistry, @specification, @cost)";

        foreach (var slot in squad.Slots)
            Execute(query, new Dictionary<string, object>
            {
                ["@approval"] = approvalId,
                ["@index"] = slot.Index,
                ["@position"] = slot.Position,
                ["@club"] = slot.Player.Owned ? slot.Player.Id : DBNull.Value,
                ["@name"] = slot.Player.Name,
                ["@rating"] = slot.Player.Rating,
                ["@owned"] = slot.Player.Owned,
                ["@chemistry"] = squad.Assessment?.Chemistry.SlotPoints[slot.Index] ?? 0,
                ["@specification"] = slot.Gap is null ? DBNull.Value : slot.Gap.Describe(),
                ["@cost"] = slot.Gap?.EstimatedCost ?? 0
            });
    }

    public IReadOnlyList<ApprovalRecord> ReadApprovals()
    {
        const string query =
            "SELECT a.id, a.challenge_id, a.challenge_name, a.formation, a.squad_rating, a.chemistry, a.purchase_count, a.estimated_cost, a.state, a.approved_at FROM SbcApprovals a LEFT JOIN SbcFulfilments f ON f.approval_id = a.id WHERE COALESCE(f.state, '') <> 'built' ORDER BY a.approved_at DESC";

        return Read(query, [], reader => new ApprovalRecord(reader.GetInt32(0), reader.GetInt32(1),
            reader.GetString(2), reader.GetString(3), reader.GetInt32(4), reader.GetInt32(5), reader.GetInt32(6),
            reader.GetInt64(7), reader.GetString(8), reader.GetDateTime(9)));
    }

    public int CountCompletedApprovals()
    {
        return Scalar("SELECT COUNT(*) FROM SbcApprovals a JOIN SbcFulfilments f ON f.approval_id = a.id "
            + "WHERE f.state = 'built'", []);
    }

    public IReadOnlyList<ApprovalSlot> ReadApprovalSlots(int approvalId)
    {
        const string query =
            "SELECT slot_index, position, club_player_id, player_name, rating, owned, specification, estimated_cost FROM SbcApprovalSlots WHERE approval_id = @approval ORDER BY slot_index";

        return Read(query, new Dictionary<string, object> { ["@approval"] = approvalId },
            reader => new ApprovalSlot(reader.GetInt32(0), reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetInt64(2), reader.GetString(3), reader.GetInt32(4),
                reader.GetBoolean(5), reader.IsDBNull(6) ? null : reader.GetString(6), reader.GetInt32(7)));
    }

    public CardAttributes? ReadCard(int assetId)
    {
        const string query =
            "SELECT club_id, league_id, nation_id, rarity_id, rating FROM Players WHERE asset_id = @asset AND club_id IS NOT NULL ORDER BY last_updated DESC LIMIT 1";

        return Read(query, new Dictionary<string, object> { ["@asset"] = assetId },
            reader => new CardAttributes(Whole(reader, 0), Whole(reader, 1), Whole(reader, 2), Whole(reader, 3),
                Whole(reader, 4))).FirstOrDefault();
    }

    private static int Whole(MySqlDataReader reader, int column)
    {
        return reader.IsDBNull(column) ? 0 : (int)reader.GetInt64(column);
    }

    public ChallengeRoute? ReadChallengeRoute(int challengeId)
    {
        const string query =
            "SELECT c.challenge_id, COALESCE(s.name, ''), c.name FROM SbcChallenges c LEFT JOIN SbcSets s ON s.set_id = c.set_id WHERE c.challenge_id = @challenge";

        return Read(query, new Dictionary<string, object> { ["@challenge"] = challengeId },
            reader => new ChallengeRoute(reader.GetInt32(0), reader.GetString(1), reader.GetString(2)))
            .FirstOrDefault();
    }

    public IReadOnlyList<SquadPlayer> ReadEveryClubPlayer()
    {
        const string query =
            "SELECT c.id, c.asset_id, COALESCE(NULLIF(p.common_name, ''), p.name, CONCAT('Asset ', c.asset_id)), c.rating, c.preferred_position, c.possible_positions, c.team_id, c.league_id, c.nation, c.rare_flag, c.untradeable, c.market_average FROM ClubPlayers c LEFT JOIN Players p ON p.asset_id = c.asset_id ORDER BY c.rating DESC, c.id";

        return Read(query, [], reader => new SquadPlayer(reader.GetInt64(0), reader.GetInt32(1), reader.GetString(2),
            reader.GetInt32(3), reader.GetString(4), Positions(reader.GetString(5)), reader.GetInt32(6),
            reader.GetInt32(7), reader.GetInt32(8), reader.GetInt32(9), reader.GetBoolean(10), reader.GetInt32(11),
            true));
    }

    public IReadOnlyList<long> ReadExclusions()
    {
        return Read("SELECT club_player_id FROM ExcludedClubPlayers", [], reader => reader.GetInt64(0));
    }

    public void Exclude(long clubPlayerId)
    {
        Execute("INSERT IGNORE INTO ExcludedClubPlayers (club_player_id) VALUES (@player)",
            new Dictionary<string, object> { ["@player"] = clubPlayerId });
    }

    public void Include(long clubPlayerId)
    {
        Execute("DELETE FROM ExcludedClubPlayers WHERE club_player_id = @player",
            new Dictionary<string, object> { ["@player"] = clubPlayerId });
    }

    public void RemoveApproval(int approvalId)
    {
        Dictionary<string, object> parameters = new() { ["@approval"] = approvalId };

        Execute("DELETE p FROM SbcFulfilmentPlacements p JOIN SbcFulfilments f ON f.id = p.fulfilment_id "
            + "WHERE f.approval_id = @approval", parameters);
        Execute("DELETE g FROM SbcFulfilmentGaps g JOIN SbcFulfilments f ON f.id = g.fulfilment_id "
            + "WHERE f.approval_id = @approval", parameters);
        Execute("DELETE FROM SbcFulfilments WHERE approval_id = @approval", parameters);
        Execute("DELETE FROM SbcApprovalSlots WHERE approval_id = @approval", parameters);
        Execute("DELETE FROM SbcApprovals WHERE id = @approval", parameters);
    }

    private List<T> Read<T>(string query, Dictionary<string, object> parameters, Func<MySqlDataReader, T> map)
    {
        List<T> result = [];
        using MySqlConnection connection = new(connectionString_);
        using MySqlCommand command = new(query, connection);

        foreach (var (key, value) in parameters) command.Parameters.AddWithValue(key, value);

        connection.Open();
        using var reader = command.ExecuteReader();
        while (reader.Read()) result.Add(map(reader));

        return result;
    }

    private void Execute(string query, Dictionary<string, object> parameters)
    {
        using MySqlConnection connection = new(connectionString_);
        using MySqlCommand command = new(query, connection);

        foreach (var (key, value) in parameters) command.Parameters.AddWithValue(key, value);

        connection.Open();
        command.ExecuteNonQuery();
    }

    private int Scalar(string query, Dictionary<string, object> parameters)
    {
        using MySqlConnection connection = new(connectionString_);
        using MySqlCommand command = new(query, connection);

        foreach (var (key, value) in parameters) command.Parameters.AddWithValue(key, value);

        connection.Open();
        var identity = command.ExecuteScalar();

        return identity is null ? 0 : Convert.ToInt32(identity);
    }
}
