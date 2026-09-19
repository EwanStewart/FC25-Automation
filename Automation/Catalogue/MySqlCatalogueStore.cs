using MySql.Data.MySqlClient;

namespace Automation.Catalogue;

public sealed class MySqlCatalogueStore : ICatalogueStore
{
    public const string DEFAULT_CONNECTION_STRING = "Server=localhost;Database=fc25;Uid=root;Pwd=root;";

    private const string UPSERT_PLAYER =
        "INSERT INTO Players (source, source_id, asset_id, resource_id, name, common_name, rating, preferred_position, alternate_positions, club_id, league_id, nation_id, rarity_id) " +
        "VALUES (@source, @sourceId, @assetId, @resourceId, @name, @commonName, @rating, @preferredPosition, @alternatePositions, @clubId, @leagueId, @nationId, @rarityId) AS incoming " +
        "ON DUPLICATE KEY UPDATE asset_id = incoming.asset_id, resource_id = incoming.resource_id, name = incoming.name, common_name = incoming.common_name, rating = incoming.rating, preferred_position = incoming.preferred_position, alternate_positions = incoming.alternate_positions, club_id = incoming.club_id, league_id = incoming.league_id, nation_id = incoming.nation_id, rarity_id = incoming.rarity_id, last_updated = CURRENT_TIMESTAMP";

    private const string SELECT_LATEST_IMPORT =
        "SELECT id, last_page, outcome FROM CatalogueImports WHERE source = @source ORDER BY id DESC LIMIT 1";

    private const string INSERT_IMPORT = "INSERT INTO CatalogueImports (source) VALUES (@source)";

    private const string UPDATE_PROGRESS =
        "UPDATE CatalogueImports SET last_page = @page, page_total = @pageTotal, item_count = item_count + @itemCount WHERE id = @id";

    private const string FINISH_IMPORT =
        "UPDATE CatalogueImports SET outcome = @outcome, finished_at = NOW() WHERE id = @id";

    private readonly string connectionString_;

    public MySqlCatalogueStore() : this(DEFAULT_CONNECTION_STRING)
    {
    }

    public MySqlCatalogueStore(string connectionString)
    {
        connectionString_ = connectionString;
    }

    public ImportProgress? LatestImport(string source)
    {
        ImportProgress? result = null;
        using var connection = Open();
        using MySqlCommand command = new(SELECT_LATEST_IMPORT, connection);
        command.Parameters.AddWithValue("@source", source);

        using var reader = command.ExecuteReader();
        if (reader.Read()) result = new ImportProgress(reader.GetInt64(0), reader.GetInt32(1), reader.GetString(2));

        return result;
    }

    public long BeginImport(string source)
    {
        using var connection = Open();
        using MySqlCommand command = new(INSERT_IMPORT, connection);
        command.Parameters.AddWithValue("@source", source);
        command.ExecuteNonQuery();

        return command.LastInsertedId;
    }

    public void SavePlayers(string source, IReadOnlyList<PlayerRecord> players)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();

        foreach (var player in players) SavePlayer(connection, transaction, source, player);

        transaction.Commit();
    }

    public void RecordProgress(long importId, int page, int pageTotal, int itemCount)
    {
        using var connection = Open();
        using MySqlCommand command = new(UPDATE_PROGRESS, connection);
        command.Parameters.AddWithValue("@page", page);
        command.Parameters.AddWithValue("@pageTotal", pageTotal);
        command.Parameters.AddWithValue("@itemCount", itemCount);
        command.Parameters.AddWithValue("@id", importId);
        command.ExecuteNonQuery();
    }

    public void FinishImport(long importId, string outcome)
    {
        using var connection = Open();
        using MySqlCommand command = new(FINISH_IMPORT, connection);
        command.Parameters.AddWithValue("@outcome", outcome);
        command.Parameters.AddWithValue("@id", importId);
        command.ExecuteNonQuery();
    }

    private static void SavePlayer(MySqlConnection connection, MySqlTransaction transaction, string source,
        PlayerRecord player)
    {
        using MySqlCommand command = new(UPSERT_PLAYER, connection, transaction);
        command.Parameters.AddWithValue("@source", source);
        command.Parameters.AddWithValue("@sourceId", player.SourceId);
        command.Parameters.AddWithValue("@assetId", Value(player.AssetId));
        command.Parameters.AddWithValue("@resourceId", Value(player.ResourceId));
        command.Parameters.AddWithValue("@name", player.Name);
        command.Parameters.AddWithValue("@commonName", Value(player.CommonName));
        command.Parameters.AddWithValue("@rating", Value(player.Rating));
        command.Parameters.AddWithValue("@preferredPosition", Value(player.PreferredPosition));
        command.Parameters.AddWithValue("@alternatePositions", Value(CatalogueRow.AlternatePositions(player)));
        command.Parameters.AddWithValue("@clubId", Value(player.ClubId));
        command.Parameters.AddWithValue("@leagueId", Value(player.LeagueId));
        command.Parameters.AddWithValue("@nationId", Value(player.NationId));
        command.Parameters.AddWithValue("@rarityId", Value(player.RarityId));
        command.ExecuteNonQuery();
    }

    private static object Value(object? value)
    {
        return value ?? DBNull.Value;
    }

    private MySqlConnection Open()
    {
        MySqlConnection result = new(connectionString_);
        result.Open();

        return result;
    }
}
