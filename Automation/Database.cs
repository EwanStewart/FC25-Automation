using Automation.Flow;
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