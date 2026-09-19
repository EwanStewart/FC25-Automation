using Automation.Flow;
using Automation.Trading;
using MySql.Data.MySqlClient;

namespace Automation;

public static class Database
{
    private const string Server = "localhost";
    private const string Db = "fc25";
    private const string User = "root";
    private const string Password = "root";
    private static readonly string ConnectionString;

    static Database()
    {
        ConnectionString = GetConnectionString();
    }

    /// <summary>
    /// Form and return connection string.
    /// </summary>
    /// <returns></returns>
    private static string GetConnectionString()
    {
        return $"Server={Server};Database={Db};Uid={User};Pwd={Password};";
    }

    /// <summary>
    /// Add Coin total to DB.
    /// </summary>
    /// <param name="total"></param>
    public static void AddToCoinTable(uint total)
    {
        const string query = "INSERT INTO CoinTotal (total) VALUES (@total);";

        using MySqlConnection connection = new(ConnectionString);
        try
        {
            connection.Open();

            using var cmd = new MySqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@total", total);

            cmd.ExecuteNonQuery();
        }
        catch (MySqlException ex)
        {
            Console.WriteLine($"MySQL Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Add sale to DB.
    /// </summary>
    /// <param name="total"></param>
    /// <param name="name"></param>
    public static void AddToSoldTable(decimal total, string name)
    {
        using MySqlConnection connection = new(ConnectionString);
        try
        {
            const string query = "INSERT INTO ItemSales (name, price) VALUES (@name, @price)";

            using MySqlCommand cmd = new(query, connection);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@price", total);

            connection.Open();
            cmd.ExecuteNonQuery();
        }
        catch (MySqlException ex)
        {
            Console.WriteLine($"MySQL Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Add sale to DB.
    /// </summary>
    /// <param name="total"></param>
    /// <param name="name"></param>
    public static void AddToSeenTable(decimal total, string name)
    {
        using MySqlConnection connection = new(ConnectionString);
        try
        {
            const string query = "INSERT INTO ItemSeenPrice (name, price) VALUES (@name, @price)";

            using MySqlCommand cmd = new(query, connection);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@price", total);

            connection.Open();
            cmd.ExecuteNonQuery();
        }
        catch (MySqlException ex)
        {
            Console.WriteLine($"MySQL Error: {ex.Message}");
        }
    }

    public static List<(uint price, DateTime timestamp)> GetRecentSightings(string name, uint days)
    {
        List<(uint, DateTime)> result = [];
        using MySqlConnection connection = new(ConnectionString);
        try
        {
            const string query =
                "SELECT price, timestamp FROM ItemSeenPrice WHERE name = @name AND timestamp >= DATE_SUB(NOW(), INTERVAL @days DAY) ORDER BY timestamp DESC";

            using MySqlCommand cmd = new(query, connection);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@days", days);

            connection.Open();
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) result.Add(((uint)reader.GetInt32(0), reader.GetDateTime(1)));
        }
        catch (MySqlException ex)
        {
            Console.WriteLine($"MySQL Error: {ex.Message}");
        }

        return result;
    }

    public static void AddBid(string name, uint bid, uint resaleEstimate, BidContext context, string segment)
    {
        using MySqlConnection connection = new(ConnectionString);
        try
        {
            const string query =
                "INSERT INTO Bids (name, bid, resale_estimate, minimum_bid, current_bid, buy_now, minutes_left, ask_count, segment) VALUES (@name, @bid, @resale, @minimum, @current, @buyNow, @minutes, @asks, @segment)";

            using MySqlCommand cmd = new(query, connection);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@bid", bid);
            cmd.Parameters.AddWithValue("@resale", resaleEstimate);
            cmd.Parameters.AddWithValue("@minimum", context.MinimumBid);
            cmd.Parameters.AddWithValue("@current", context.CurrentBid);
            cmd.Parameters.AddWithValue("@buyNow", context.BuyNow);
            cmd.Parameters.AddWithValue("@minutes", context.MinutesLeft);
            cmd.Parameters.AddWithValue("@asks", context.AskCount);
            cmd.Parameters.AddWithValue("@segment", segment);

            connection.Open();
            cmd.ExecuteNonQuery();
        }
        catch (MySqlException ex)
        {
            Console.WriteLine($"MySQL Error: {ex.Message}");
        }
    }

    public static void AddCompareRead(string name, IReadOnlyList<uint> asks, int pageCount)
    {
        using MySqlConnection connection = new(ConnectionString);
        try
        {
            const string query = "INSERT INTO CompareReads (name, asks, page_count) VALUES (@name, @asks, @pages)";

            using MySqlCommand cmd = new(query, connection);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@asks", string.Join(",", asks));
            cmd.Parameters.AddWithValue("@pages", pageCount);

            connection.Open();
            cmd.ExecuteNonQuery();
        }
        catch (MySqlException ex)
        {
            Console.WriteLine($"MySQL Error: {ex.Message}");
        }
    }

    public static uint? GetLatestBidPrice(string name, uint days)
    {
        uint? result = null;
        using MySqlConnection connection = new(ConnectionString);
        try
        {
            const string query =
                "SELECT bid FROM Bids WHERE name = @name AND timestamp >= DATE_SUB(NOW(), INTERVAL @days DAY) ORDER BY timestamp DESC LIMIT 1";

            using MySqlCommand cmd = new(query, connection);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@days", days);

            connection.Open();
            var value = cmd.ExecuteScalar();
            if (value != null) result = Convert.ToUInt32(value);
        }
        catch (MySqlException ex)
        {
            Console.WriteLine($"MySQL Error: {ex.Message}");
        }

        return result;
    }

    public static void MarkLatestBidListed(string name, uint listedPrice)
    {
        using MySqlConnection connection = new(ConnectionString);
        try
        {
            const string query =
                "UPDATE Bids SET listed_price = @price WHERE name = @name AND outcome IN ('won', 'open') ORDER BY timestamp DESC LIMIT 1";

            using MySqlCommand cmd = new(query, connection);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@price", listedPrice);

            connection.Open();
            cmd.ExecuteNonQuery();
        }
        catch (MySqlException ex)
        {
            Console.WriteLine($"MySQL Error: {ex.Message}");
        }
    }

    public static void MarkLatestOpenBidWon(string name)
    {
        const string query =
            "UPDATE Bids SET outcome = 'won', resolved_at = NOW() WHERE name = @name AND outcome = 'open' ORDER BY timestamp DESC LIMIT 1";

        ExecuteWithName(query, name);
    }

    public static void MarkLatestOpenBidLost(string name)
    {
        const string query =
            "UPDATE Bids SET outcome = 'lost', resolved_at = NOW() WHERE name = @name AND outcome = 'open' ORDER BY timestamp DESC LIMIT 1";

        ExecuteWithName(query, name);
    }

    public static void RaiseOpenBid(string name, uint amount)
    {
        using MySqlConnection connection = new(ConnectionString);
        try
        {
            const string query =
                "UPDATE Bids SET bid = @amount, current_bid = @amount, rebids = rebids + 1 WHERE name = @name AND outcome = 'open' ORDER BY timestamp DESC LIMIT 1";

            using MySqlCommand cmd = new(query, connection);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@amount", amount);

            connection.Open();
            cmd.ExecuteNonQuery();
        }
        catch (MySqlException ex)
        {
            Console.WriteLine($"MySQL Error: {ex.Message}");
        }
    }

    public static void AddSnipeEvent(string name, string eventName, uint? amount, uint? minimumBid, uint? estimate,
        string? timeLeft, string? detail)
    {
        using MySqlConnection connection = new(ConnectionString);
        try
        {
            const string query =
                "INSERT INTO SnipeEvents (name, event, amount, minimum_bid, estimate, time_left, detail) VALUES (@name, @event, @amount, @minimum, @estimate, @timeLeft, @detail)";

            using MySqlCommand cmd = new(query, connection);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@event", eventName);
            cmd.Parameters.AddWithValue("@amount", amount);
            cmd.Parameters.AddWithValue("@minimum", minimumBid);
            cmd.Parameters.AddWithValue("@estimate", estimate);
            cmd.Parameters.AddWithValue("@timeLeft", timeLeft);
            cmd.Parameters.AddWithValue("@detail", detail);

            connection.Open();
            cmd.ExecuteNonQuery();
        }
        catch (MySqlException ex)
        {
            Console.WriteLine($"MySQL Error: {ex.Message}");
        }
    }

    public static void MarkLatestWonBidSold(string name, uint soldPrice)
    {
        using MySqlConnection connection = new(ConnectionString);
        try
        {
            const string query =
                "UPDATE Bids SET outcome = 'sold', sold_price = @price, resolved_at = NOW() WHERE name = @name AND outcome = 'won' ORDER BY resolved_at DESC LIMIT 1";

            using MySqlCommand cmd = new(query, connection);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@price", soldPrice);

            connection.Open();
            cmd.ExecuteNonQuery();
        }
        catch (MySqlException ex)
        {
            Console.WriteLine($"MySQL Error: {ex.Message}");
        }
    }

    public static void MarkStaleOpenBidsLost(uint olderThanHours)
    {
        using MySqlConnection connection = new(ConnectionString);
        try
        {
            const string query =
                "UPDATE Bids SET outcome = 'lost', resolved_at = NOW() WHERE outcome = 'open' AND timestamp < DATE_SUB(NOW(), INTERVAL @hours HOUR)";

            using MySqlCommand cmd = new(query, connection);
            cmd.Parameters.AddWithValue("@hours", olderThanHours);

            connection.Open();
            cmd.ExecuteNonQuery();
        }
        catch (MySqlException ex)
        {
            Console.WriteLine($"MySQL Error: {ex.Message}");
        }
    }

    public static uint? GetLatestWonBidPrice(string name)
    {
        uint? result = null;
        using MySqlConnection connection = new(ConnectionString);
        try
        {
            const string query =
                "SELECT bid FROM Bids WHERE name = @name AND outcome = 'won' ORDER BY resolved_at DESC LIMIT 1";

            using MySqlCommand cmd = new(query, connection);
            cmd.Parameters.AddWithValue("@name", name);

            connection.Open();
            var value = cmd.ExecuteScalar();
            if (value != null) result = Convert.ToUInt32(value);
        }
        catch (MySqlException ex)
        {
            Console.WriteLine($"MySQL Error: {ex.Message}");
        }

        return result;
    }

    public static List<uint> GetRecentSales(string name, uint days)
    {
        const string query =
            "SELECT price FROM ItemSales WHERE name = @name AND timestamp >= DATE_SUB(NOW(), INTERVAL @days DAY) ORDER BY timestamp DESC";

        return ReadRows(query, new() { ["@name"] = name, ["@days"] = days }, reader => (uint)reader.GetInt32(0));
    }

    public static List<(uint sold, uint estimate)> GetSegmentSaleRatios(string segment, uint days)
    {
        const string query =
            "SELECT sold_price, resale_estimate FROM Bids WHERE segment = @segment AND outcome = 'sold' AND sold_price IS NOT NULL AND resolved_at >= DATE_SUB(NOW(), INTERVAL @days DAY)";

        return ReadRows(query, new() { ["@segment"] = segment, ["@days"] = days },
            reader => ((uint)reader.GetInt32(0), (uint)reader.GetInt32(1)));
    }

    public static Dictionary<string, SegmentRecord> GetSegmentRecords(uint days)
    {
        const string query =
            "SELECT segment, SUM(outcome IN ('won', 'sold')), SUM(outcome = 'lost'), COALESCE(SUM(CASE WHEN outcome = 'sold' THEN FLOOR(sold_price * @keep) - bid ELSE 0 END), 0), MAX(timestamp) FROM Bids WHERE segment IS NOT NULL AND timestamp >= DATE_SUB(NOW(), INTERVAL @days DAY) GROUP BY segment";
        var rows = ReadRows(query, new() { ["@days"] = days, ["@keep"] = 1 - Pricing.TAX_RATE },
            reader => new SegmentRecord(reader.GetString(0), Convert.ToInt32(reader.GetValue(1)),
                Convert.ToInt32(reader.GetValue(2)), Convert.ToInt64(reader.GetValue(3)), reader.GetDateTime(4)));

        return rows.ToDictionary(record => record.Segment);
    }

    public static void AddPass(string segment, string role, int rowsConsidered, int compareReads, uint bids,
        int seconds)
    {
        using MySqlConnection connection = new(ConnectionString);
        try
        {
            const string query =
                "INSERT INTO Passes (segment, role, rows_considered, compare_reads, bids, seconds) VALUES (@segment, @role, @rows, @reads, @bids, @seconds)";

            using MySqlCommand cmd = new(query, connection);
            cmd.Parameters.AddWithValue("@segment", segment);
            cmd.Parameters.AddWithValue("@role", role);
            cmd.Parameters.AddWithValue("@rows", rowsConsidered);
            cmd.Parameters.AddWithValue("@reads", compareReads);
            cmd.Parameters.AddWithValue("@bids", bids);
            cmd.Parameters.AddWithValue("@seconds", seconds);

            connection.Open();
            cmd.ExecuteNonQuery();
        }
        catch (MySqlException ex)
        {
            Console.WriteLine($"MySQL Error: {ex.Message}");
        }
    }

    public static string? GetLastExploratoryNation()
    {
        const string query = "SELECT segment FROM Passes WHERE role = @role ORDER BY id DESC LIMIT 1";
        var segments = ReadRows(query, new() { ["@role"] = Segments.ROLE_EXPLORE }, reader => reader.GetString(0));

        return segments.Count > 0 ? Segments.Nation(segments[0]) : null;
    }

    public static string? GetLastSnipeFilterName()
    {
        const string query = "SELECT name FROM SnipeEvents WHERE event = 'search' ORDER BY id DESC LIMIT 1";
        var names = ReadRows(query, new(), reader => reader.GetString(0));

        return names.Count > 0 ? names[0] : null;
    }

    public static void UpsertClubPlayers(IReadOnlyList<ClubPlayer> players)
    {
        using MySqlConnection connection = new(ConnectionString);
        try
        {
            connection.Open();

            foreach (var player in players) UpsertClubPlayer(connection, player);
        }
        catch (MySqlException ex)
        {
            Console.WriteLine($"MySQL Error: {ex.Message}");
        }
    }

    private static void UpsertClubPlayer(MySqlConnection connection, ClubPlayer player)
    {
        const string query =
            "INSERT INTO ClubPlayers (id, asset_id, resource_id, rating, preferred_position, possible_positions, team_id, league_id, nation, rare_flag, card_sub_type_id, untradeable, item_state, pile, market_average) VALUES (@id, @asset, @resource, @rating, @preferred, @possible, @team, @league, @nation, @rare, @subtype, @untradeable, @state, @pile, @average) AS incoming ON DUPLICATE KEY UPDATE asset_id = incoming.asset_id, resource_id = incoming.resource_id, rating = incoming.rating, preferred_position = incoming.preferred_position, possible_positions = incoming.possible_positions, team_id = incoming.team_id, league_id = incoming.league_id, nation = incoming.nation, rare_flag = incoming.rare_flag, card_sub_type_id = incoming.card_sub_type_id, untradeable = incoming.untradeable, item_state = incoming.item_state, pile = incoming.pile, market_average = incoming.market_average, captured_at = CURRENT_TIMESTAMP";

        using MySqlCommand cmd = new(query, connection);
        cmd.Parameters.AddWithValue("@id", player.Id);
        cmd.Parameters.AddWithValue("@asset", player.AssetId);
        cmd.Parameters.AddWithValue("@resource", player.ResourceId);
        cmd.Parameters.AddWithValue("@rating", player.Rating);
        cmd.Parameters.AddWithValue("@preferred", player.PreferredPosition);
        cmd.Parameters.AddWithValue("@possible", string.Join(",", player.PossiblePositions));
        cmd.Parameters.AddWithValue("@team", player.TeamId);
        cmd.Parameters.AddWithValue("@league", player.LeagueId);
        cmd.Parameters.AddWithValue("@nation", player.Nation);
        cmd.Parameters.AddWithValue("@rare", player.RareFlag);
        cmd.Parameters.AddWithValue("@subtype", player.CardSubTypeId);
        cmd.Parameters.AddWithValue("@untradeable", player.Untradeable);
        cmd.Parameters.AddWithValue("@state", player.ItemState);
        cmd.Parameters.AddWithValue("@pile", player.Pile);
        cmd.Parameters.AddWithValue("@average", player.MarketAverage);

        cmd.ExecuteNonQuery();
    }

    public static void AddClubSnapshot(int itemCount, string outcome, string detail)
    {
        using MySqlConnection connection = new(ConnectionString);
        try
        {
            const string query =
                "INSERT INTO ClubSnapshots (item_count, outcome, detail) VALUES (@count, @outcome, @detail)";

            using MySqlCommand cmd = new(query, connection);
            cmd.Parameters.AddWithValue("@count", itemCount);
            cmd.Parameters.AddWithValue("@outcome", outcome);
            cmd.Parameters.AddWithValue("@detail", detail);

            connection.Open();
            cmd.ExecuteNonQuery();
        }
        catch (MySqlException ex)
        {
            Console.WriteLine($"MySQL Error: {ex.Message}");
        }
    }

    public static int CountClubPlayers()
    {
        const string query = "SELECT COUNT(*) FROM ClubPlayers";
        var counts = ReadRows(query, new(), reader => reader.GetInt32(0));

        return counts.Count > 0 ? counts[0] : 0;
    }

    private static List<T> ReadRows<T>(string query, Dictionary<string, object> parameters,
        Func<MySqlDataReader, T> map)
    {
        List<T> result = [];
        using MySqlConnection connection = new(ConnectionString);
        try
        {
            using MySqlCommand cmd = new(query, connection);
            foreach (var (key, value) in parameters) cmd.Parameters.AddWithValue(key, value);

            connection.Open();
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) result.Add(map(reader));
        }
        catch (MySqlException ex)
        {
            Console.WriteLine($"MySQL Error: {ex.Message}");
        }

        return result;
    }

    private static void ExecuteWithName(string query, string name)
    {
        using MySqlConnection connection = new(ConnectionString);
        try
        {
            using MySqlCommand cmd = new(query, connection);
            cmd.Parameters.AddWithValue("@name", name);

            connection.Open();
            cmd.ExecuteNonQuery();
        }
        catch (MySqlException ex)
        {
            Console.WriteLine($"MySQL Error: {ex.Message}");
        }
    }
}