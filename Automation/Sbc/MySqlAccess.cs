using MySql.Data.MySqlClient;

namespace Automation.Sbc;

public sealed class MySqlAccess
{
    private readonly string connectionString_;

    public MySqlAccess(string connectionString)
    {
        connectionString_ = connectionString;
    }

    public List<T> Read<T>(string query, Dictionary<string, object> parameters, Func<MySqlDataReader, T> map)
    {
        List<T> result = [];
        using MySqlConnection connection = new(connectionString_);
        using MySqlCommand command = new(query, connection);

        Bind(command, parameters);
        connection.Open();

        using var reader = command.ExecuteReader();

        while (reader.Read()) result.Add(map(reader));

        return result;
    }

    public void Execute(string query, Dictionary<string, object> parameters)
    {
        using MySqlConnection connection = new(connectionString_);
        using MySqlCommand command = new(query, connection);

        Bind(command, parameters);
        connection.Open();
        command.ExecuteNonQuery();
    }

    public int Scalar(string query, Dictionary<string, object> parameters)
    {
        using MySqlConnection connection = new(connectionString_);
        using MySqlCommand command = new(query, connection);

        Bind(command, parameters);
        connection.Open();

        var identity = command.ExecuteScalar();

        return identity is null or DBNull ? 0 : Convert.ToInt32(identity);
    }

    private static void Bind(MySqlCommand command, Dictionary<string, object> parameters)
    {
        foreach (var (key, value) in parameters) command.Parameters.AddWithValue(key, value);
    }
}
