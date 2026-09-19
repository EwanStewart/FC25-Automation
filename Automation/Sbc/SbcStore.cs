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
            "SELECT c.id, c.asset_id, COALESCE(NULLIF(p.common_name, ''), p.name, CONCAT('Asset ', c.asset_id)), c.rating, c.preferred_position, c.possible_positions, c.team_id, c.league_id, c.nation, c.rare_flag, c.untradeable, c.market_average FROM ClubPlayers c LEFT JOIN Players p ON p.asset_id = c.asset_id ORDER BY c.rating DESC, c.id";

        return Read(query, [], reader => new SquadPlayer(reader.GetInt64(0), reader.GetInt32(1), reader.GetString(2),
            reader.GetInt32(3), reader.GetString(4), Positions(reader.GetString(5)), reader.GetInt32(6),
            reader.GetInt32(7), reader.GetInt32(8), reader.GetInt32(9), reader.GetBoolean(10), reader.GetInt32(11),
            true));
    }

    private static IReadOnlyList<string> Positions(string csv)
    {
        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
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
            "SELECT id, challenge_id, challenge_name, formation, squad_rating, chemistry, purchase_count, estimated_cost, state, approved_at FROM SbcApprovals ORDER BY approved_at DESC";

        return Read(query, [], reader => new ApprovalRecord(reader.GetInt32(0), reader.GetInt32(1),
            reader.GetString(2), reader.GetString(3), reader.GetInt32(4), reader.GetInt32(5), reader.GetInt32(6),
            reader.GetInt64(7), reader.GetString(8), reader.GetDateTime(9)));
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
