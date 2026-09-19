using System.Text.Json;

namespace Automation.Trading;

public sealed record ClubPlayer(
    long Id,
    int AssetId,
    int ResourceId,
    int Rating,
    string PreferredPosition,
    IReadOnlyList<string> PossiblePositions,
    int TeamId,
    int LeagueId,
    int Nation,
    int RareFlag,
    int CardSubTypeId,
    bool Untradeable,
    string ItemState,
    int Pile,
    int MarketAverage);

public static class ClubInventory
{
    private const string PLAYER_ITEM_TYPE = "player";

    public static IReadOnlyList<ClubPlayer> Parse(string json)
    {
        List<ClubPlayer> result = [];

        try
        {
            using var document = JsonDocument.Parse(json);

            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("itemData", out var items) &&
                items.ValueKind == JsonValueKind.Array)
                result = items.EnumerateArray().Where(IsPlayer).Select(ParsePlayer).ToList();
        }
        catch (JsonException)
        {
        }

        return result;
    }

    private static bool IsPlayer(JsonElement element)
    {
        var itemType = Text(element, "itemType");

        return itemType.Length == 0 || itemType.Equals(PLAYER_ITEM_TYPE, StringComparison.Ordinal);
    }

    private static ClubPlayer ParsePlayer(JsonElement element)
    {
        return new ClubPlayer(
            Identifier(element, "id"),
            Number(element, "assetId"),
            Number(element, "resourceId"),
            Number(element, "rating"),
            Text(element, "preferredPosition"),
            Positions(element, "possiblePositions"),
            Number(element, "teamid"),
            Number(element, "leagueId"),
            Number(element, "nation"),
            Number(element, "rareflag"),
            Number(element, "cardsubtypeid"),
            Flag(element, "untradeable"),
            Text(element, "itemState"),
            Number(element, "pile"),
            Number(element, "marketAverage"));
    }

    private static IReadOnlyList<string> Positions(JsonElement element, string name)
    {
        IReadOnlyList<string> result = [];

        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.Array)
            result = value.EnumerateArray().Where(entry => entry.ValueKind == JsonValueKind.String)
                .Select(entry => entry.GetString() ?? string.Empty).ToList();

        return result;
    }

    private static string Text(JsonElement element, string name)
    {
        var result = string.Empty;

        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.String)
            result = value.GetString() ?? string.Empty;

        return result;
    }

    private static int Number(JsonElement element, string name)
    {
        return (int)Math.Clamp(Identifier(element, name), int.MinValue, int.MaxValue);
    }

    private static long Identifier(JsonElement element, string name)
    {
        var result = 0L;

        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var parsed))
            result = parsed;

        return result;
    }

    private static bool Flag(JsonElement element, string name)
    {
        var result = false;

        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value))
            result = value.ValueKind == JsonValueKind.True;

        return result;
    }
}
