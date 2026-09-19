using System.Text;
using System.Text.Json;

namespace Automation.Sbc;

public sealed record FormationLayout(string Code, int FormationId, string DisplayName, IReadOnlyList<string> Slots);

public sealed class FormationLayoutException(string message) : Exception(message);

public static class FormationLayouts
{
    public const int SQUAD_SIZE = 11;

    public static IReadOnlyList<FormationLayout> Parse(string json)
    {
        using var document = Document(json);
        var size = SquadSize(document.RootElement);
        var result = Entries(document.RootElement).Select(entry => ReadLayout(entry, size)).ToList();

        GuardAgainstRepeats(result);

        return result;
    }

    public static string ToJson(IReadOnlyList<FormationLayout> layouts, string capturedAt, string source)
    {
        var builder = new StringBuilder();

        builder.AppendLine("{");
        builder.AppendLine($"  \"capturedAt\": {Quoted(capturedAt)},");
        builder.AppendLine($"  \"source\": {Quoted(source)},");
        builder.AppendLine($"  \"squadSize\": {SQUAD_SIZE},");
        builder.AppendLine("  \"formations\": [");
        builder.AppendLine(string.Join($",{Environment.NewLine}", layouts.Select(Serialise)));
        builder.AppendLine("  ]");
        builder.AppendLine("}");

        return builder.ToString();
    }

    private static string Serialise(FormationLayout layout)
    {
        var slots = string.Join(", ", layout.Slots.Select(Quoted));

        return $"    {{{Environment.NewLine}      \"code\": {Quoted(layout.Code)},{Environment.NewLine}" +
               $"      \"id\": {layout.FormationId},{Environment.NewLine}" +
               $"      \"display\": {Quoted(layout.DisplayName)},{Environment.NewLine}" +
               $"      \"slots\": [{slots}]{Environment.NewLine}    }}";
    }

    private static string Quoted(string value)
    {
        return JsonSerializer.Serialize(value);
    }

    private static JsonDocument Document(string json)
    {
        JsonDocument result;

        try
        {
            result = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new FormationLayoutException($"the formation layouts could not be read: {exception.Message}");
        }

        return result;
    }

    private static int SquadSize(JsonElement root)
    {
        var result = SQUAD_SIZE;

        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("squadSize", out var size) &&
            size.ValueKind == JsonValueKind.Number)
            result = size.GetInt32();

        return result;
    }

    private static IEnumerable<JsonElement> Entries(JsonElement root)
    {
        IEnumerable<JsonElement> result;

        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("formations", out var formations) &&
            formations.ValueKind == JsonValueKind.Array)
            result = formations.EnumerateArray().ToList();
        else
            throw new FormationLayoutException("the formation layouts could not be read: no formations array");

        return result;
    }

    private static FormationLayout ReadLayout(JsonElement entry, int size)
    {
        var code = Text(entry, "code");
        var slots = Slots(entry, code, size);

        if (code.Length == 0) throw new FormationLayoutException("a captured formation carries no code");

        return new FormationLayout(code, Number(entry, "id"), Text(entry, "display"), slots);
    }

    private static IReadOnlyList<string> Slots(JsonElement entry, string code, int size)
    {
        List<string> result = [];

        if (entry.ValueKind == JsonValueKind.Object && entry.TryGetProperty("slots", out var slots) &&
            slots.ValueKind == JsonValueKind.Array)
            result = slots.EnumerateArray().Select(slot => slot.GetString() ?? string.Empty).ToList();

        if (result.Count != size)
            throw new FormationLayoutException($"formation {code} carries {result.Count} slots, not {size}");

        return result;
    }

    private static void GuardAgainstRepeats(IReadOnlyList<FormationLayout> layouts)
    {
        var repeated = layouts.GroupBy(layout => layout.Code, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1).Select(group => group.Key).ToList();

        if (repeated.Count > 0)
            throw new FormationLayoutException($"{string.Join(", ", repeated)} appears twice in the captured set");
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
        var result = 0;

        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed))
            result = parsed;

        return result;
    }
}
