using System.Text.Json;

namespace Automation.Catalogue;

public static class CatalogueParser
{
    private const string PAGINATION = "pagination";
    private const string ITEMS = "items";
    private const string PAGE_CURRENT = "pageCurrent";
    private const string PAGE_TOTAL = "pageTotal";
    private const string COUNT_TOTAL = "countTotal";
    private const string SOURCE_ID = "id";

    public static CataloguePage ParsePage(string json)
    {
        using var document = Parse(json);
        var root = document.RootElement;
        var paging = Section(root, PAGINATION);
        var result = new CataloguePage(Number(paging, PAGE_CURRENT), Number(paging, PAGE_TOTAL),
            Number(paging, COUNT_TOTAL), ReadPlayers(Section(root, ITEMS)));

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
            throw new CatalogueFormatException("The catalogue page was not valid JSON.", error);
        }

        return result;
    }

    private static JsonElement Section(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var section))
            throw new CatalogueFormatException($"The catalogue page has no {name} section.");

        return section;
    }

    private static int Number(JsonElement section, string name)
    {
        if (!section.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number)
            throw new CatalogueFormatException($"The catalogue paging has no {name}.");

        return value.GetInt32();
    }

    private static IReadOnlyList<PlayerRecord> ReadPlayers(JsonElement items)
    {
        if (items.ValueKind != JsonValueKind.Array)
            throw new CatalogueFormatException("The catalogue page items were not an array.");

        return items.EnumerateArray().Where(HasSourceId).Select(ReadPlayer).ToList();
    }

    private static bool HasSourceId(JsonElement item)
    {
        return NullableInteger(item, SOURCE_ID).HasValue;
    }

    private static PlayerRecord ReadPlayer(JsonElement item)
    {
        var result = new PlayerRecord(NullableInteger(item, SOURCE_ID) ?? 0, NullableLong(item, "resourceId"),
            NullableInteger(item, "resourceBaseId"), Text(item, "name") ?? string.Empty, Text(item, "commonName"),
            NullableInteger(item, "rating"),
            Text(item, "position"), Texts(item, "positionAlternatives"), NullableInteger(item, "club"),
            NullableInteger(item, "league"), NullableInteger(item, "nation"), NullableInteger(item, "rarity"),
            Text(item, "color"));

        return result;
    }

    private static int? NullableInteger(JsonElement item, string name)
    {
        var present = item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number;

        return present ? value.GetInt32() : null;
    }

    private static long? NullableLong(JsonElement item, string name)
    {
        var present = item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number;

        return present ? value.GetInt64() : null;
    }

    private static string? Text(JsonElement item, string name)
    {
        var present = item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String;

        return present ? value.GetString() : null;
    }

    private static IReadOnlyList<string> Texts(JsonElement item, string name)
    {
        var present = item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array;

        return present ? value.EnumerateArray().Select(Entry).Where(entry => entry.Length > 0).ToList() : [];
    }

    private static string Entry(JsonElement value)
    {
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
    }
}
