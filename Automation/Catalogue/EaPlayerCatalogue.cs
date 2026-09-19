using System.Text.Json;

namespace Automation.Catalogue;

public static class EaPlayerCatalogue
{
    private const string PLAYERS = "Players";
    private const string LEGENDS = "LegendsPlayers";
    private const string RECORD_ID = "id";
    private const string FIRST_NAME = "f";
    private const string LAST_NAME = "l";
    private const string COMMON_NAME = "c";
    private const string RATING = "r";

    public static IReadOnlyList<PlayerRecord> ParsePlayers(string json)
    {
        using var document = Parse(json);
        var root = document.RootElement;
        var result = ReadArray(root, PLAYERS).Concat(ReadArray(root, LEGENDS)).ToList();

        if (result.Count == 0 && !HasEitherArray(root))
            throw new CatalogueFormatException($"The player file carries neither {PLAYERS} nor {LEGENDS}.");

        return result;
    }

    private static JsonDocument Parse(string json)
    {
        JsonDocument result;

        try
        {
            result = JsonDocument.Parse(json);
        }
        catch (JsonException error)
        {
            throw new CatalogueFormatException("The player file was not valid JSON.", error);
        }

        return result;
    }

    private static bool HasEitherArray(JsonElement root)
    {
        return root.TryGetProperty(PLAYERS, out _) || root.TryGetProperty(LEGENDS, out _);
    }

    private static IEnumerable<PlayerRecord> ReadArray(JsonElement root, string name)
    {
        var present = root.TryGetProperty(name, out var array) && array.ValueKind == JsonValueKind.Array;

        return present ? array.EnumerateArray().Where(HasId).Select(ReadPlayer) : [];
    }

    private static bool HasId(JsonElement item)
    {
        return NullableLong(item, RECORD_ID).HasValue;
    }

    private static PlayerRecord ReadPlayer(JsonElement item)
    {
        var assetId = NullableLong(item, RECORD_ID) ?? 0;

        return new PlayerRecord(assetId, null, assetId, FullName(item), Text(item, COMMON_NAME),
            NullableInteger(item, RATING), null, [], null, null, null, null);
    }

    private static string FullName(JsonElement item)
    {
        var parts = new[] { Text(item, FIRST_NAME), Text(item, LAST_NAME) }.Where(part => !string.IsNullOrWhiteSpace(part));

        return string.Join(" ", parts);
    }

    private static long? NullableLong(JsonElement item, string name)
    {
        var present = item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number;

        return present ? value.GetInt64() : null;
    }

    private static int? NullableInteger(JsonElement item, string name)
    {
        var present = item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number;

        return present ? value.GetInt32() : null;
    }

    private static string? Text(JsonElement item, string name)
    {
        var present = item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String;

        return present ? value.GetString() : null;
    }
}
