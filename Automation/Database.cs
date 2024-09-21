using MySql.Data.MySqlClient;
using Automation.Definitions;

namespace Automation
{
    public static class Database
    {
        private static readonly string server = "localhost";
        private static readonly string database = "fc25";
        private static readonly string user = "root";
        private static readonly string password = "root";
        private static readonly string connectionString = "root";

        static Database ()
        {
            connectionString = GetConnectionString();
        }
        /// <summary>
        /// Form and return connection string.
        /// </summary>
        /// <returns></returns>
        private static string GetConnectionString ()
        {
            return $"Server={server};Database={database};Uid={user};Pwd={password};";
        }

        /// <summary>
        /// Add Coin total to DB.
        /// </summary>
        /// <param name="total"></param>
        public static void AddToCoinTable (uint total)
        {
            string query = "INSERT INTO CoinTotal (total) VALUES (@total);";

            using (MySqlConnection connection = new (connectionString))
            {
                try
                {
                    connection.Open ();

                    using (MySqlCommand cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@total", total);

                        cmd.ExecuteNonQuery();
                    }
                }
                catch (MySqlException ex)
                {
                    Console.WriteLine ($"MySQL Error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Add sale to DB.
        /// </summary>
        /// <param name="total"></param>
        public static void AddToSoldTable(decimal total, string name)
        {
            using (MySqlConnection connection = new(connectionString))
            {
                try
                {
                    string query = "INSERT INTO ItemSales (name, price) VALUES (@name, @price)";

                    using (MySqlCommand cmd = new(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@name", name);
                        cmd.Parameters.AddWithValue("@price", total);

                        connection.Open();
                        cmd.ExecuteNonQuery();
                    }
                }
                catch (MySqlException ex)
                {
                    Console.WriteLine($"MySQL Error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Add sale to DB.
        /// </summary>
        /// <param name="total"></param>
        public static void AddToSeenTable(decimal total, string name)
        {
            using (MySqlConnection connection = new(connectionString))
            {
                try
                {
                    string query = "INSERT INTO ItemSeenPrice (name, price) VALUES (@name, @price)";

                    using (MySqlCommand cmd = new(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@name", name);
                        cmd.Parameters.AddWithValue("@price", total);

                        connection.Open();
                        cmd.ExecuteNonQuery();
                    }
                }
                catch (MySqlException ex)
                {
                    Console.WriteLine($"MySQL Error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Check if the name has been seen at least twice today and price is less than 1000.
        /// </summary>
        /// <param name="name"></param>
        /// <returns>True if the name has been seen at least twice today and price is less than 1000, otherwise false</returns>
        public static bool HasBeenSeenTodayAndLessThanBidThreshold (string name)
        {
            using (MySqlConnection connection = new(connectionString))
            {
                try
                {
                    string query = $@"SELECT COUNT(*) FROM ItemSeenPrice WHERE name = @name AND DATE(timestamp) = CURDATE() AND price < {FC25Definitions.MIN_BUY_NOW_FOR_BID};";

                    using (MySqlCommand cmd = new(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@name", name);

                        connection.Open();
                        int count = Convert.ToInt32(cmd.ExecuteScalar()); 

                        return count > 1; 
                    }
                }
                catch (MySqlException ex)
                {
                    Console.WriteLine($"MySQL Error: {ex.Message}");
                    return false;
                }
            }
        }

    }
}
