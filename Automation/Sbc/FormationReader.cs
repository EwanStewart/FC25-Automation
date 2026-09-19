using System.Text.Json;

namespace Automation.Sbc;

public enum FormationCaptureOutcome
{
    Captured,
    Partial,
    NotObserved,
    Failed
}

public sealed record FormationEntry(int FormationId, string Code, string DisplayName);

public sealed record FormationCaptureReading(
    FormationCaptureOutcome Outcome,
    IReadOnlyList<FormationLayout> Layouts,
    IReadOnlyList<string> Unresolved,
    string Detail);

public static class FormationReader
{
    private const string UNNAMED_SLIDE = "?";

    public static FormationCaptureReading Read(string directoryJson, IReadOnlyList<string> slideJsons)
    {
        var directory = Directory(directoryJson);
        FormationCaptureReading result;

        if (directory == null)
            result = new FormationCaptureReading(FormationCaptureOutcome.Failed, [], [],
                "the formation directory could not be read");
        else if (slideJsons.Count == 0)
            result = new FormationCaptureReading(FormationCaptureOutcome.NotObserved, [], [],
                "no formation slide was observed");
        else
            result = Assemble(directory, slideJsons);

        return result;
    }

    private static FormationCaptureReading Assemble(IReadOnlyDictionary<int, FormationEntry> directory,
        IReadOnlyList<string> slideJsons)
    {
        List<FormationLayout> layouts = [];
        List<string> unresolved = [];

        foreach (var slideJson in slideJsons) Absorb(directory, slideJson, layouts, unresolved);

        return new FormationCaptureReading(Outcome(layouts, unresolved), layouts, unresolved,
            Detail(layouts, unresolved));
    }

    private static void Absorb(IReadOnlyDictionary<int, FormationEntry> directory, string slideJson,
        List<FormationLayout> layouts, List<string> unresolved)
    {
        var layout = Layout(directory, slideJson);
        var alreadyHeld = layout != null &&
                          layouts.Any(held => held.Code.Equals(layout.Code, StringComparison.OrdinalIgnoreCase));

        if (layout != null && !alreadyHeld) layouts.Add(layout);
        else if (layout == null) unresolved.Add(SlideName(slideJson));
    }

    private static FormationLayout? Layout(IReadOnlyDictionary<int, FormationEntry> directory, string slideJson)
    {
        var slots = Slots(slideJson);
        var named = int.TryParse(SlideName(slideJson), out var imageId) && directory.TryGetValue(imageId, out var entry)
            ? entry
            : null;
        FormationLayout? result = null;

        if (named != null && slots != null)
            result = new FormationLayout(named.Code, named.FormationId, named.DisplayName, slots);

        return result;
    }

    private static IReadOnlyList<string>? Slots(string slideJson)
    {
        var labels = Labels(slideJson);
        IReadOnlyList<string>? result = null;

        if (labels != null && labels.Count == FormationLayouts.SQUAD_SIZE &&
            labels.Select(pair => pair.Key).SequenceEqual(Enumerable.Range(0, FormationLayouts.SQUAD_SIZE)))
            result = labels.Select(pair => pair.Value).ToList();

        return result;
    }

    private static IReadOnlyList<KeyValuePair<int, string>>? Labels(string slideJson)
    {
        List<KeyValuePair<int, string>>? result = null;

        try
        {
            using var document = JsonDocument.Parse(slideJson);

            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("slots", out var slots) &&
                slots.ValueKind == JsonValueKind.Array)
                result = slots.EnumerateArray().Select(Label).OrderBy(pair => pair.Key).ToList();
        }
        catch (JsonException)
        {
        }

        return result;
    }

    private static KeyValuePair<int, string> Label(JsonElement slot)
    {
        var index = slot.TryGetProperty("index", out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : -1;
        var label = slot.TryGetProperty("label", out var text) && text.ValueKind == JsonValueKind.String
            ? text.GetString() ?? string.Empty
            : string.Empty;

        return new KeyValuePair<int, string>(index, label);
    }

    private static string SlideName(string slideJson)
    {
        var result = UNNAMED_SLIDE;

        try
        {
            using var document = JsonDocument.Parse(slideJson);

            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("imageId", out var value) &&
                value.ValueKind == JsonValueKind.String)
                result = value.GetString() ?? UNNAMED_SLIDE;
        }
        catch (JsonException)
        {
        }

        return result;
    }

    private static IReadOnlyDictionary<int, FormationEntry>? Directory(string directoryJson)
    {
        Dictionary<int, FormationEntry>? result = null;

        try
        {
            using var document = JsonDocument.Parse(directoryJson);

            if (document.RootElement.ValueKind == JsonValueKind.Array)
                result = document.RootElement.EnumerateArray().Select(Entry)
                    .GroupBy(entry => entry.FormationId).Select(group => group.First())
                    .ToDictionary(entry => entry.FormationId);
        }
        catch (JsonException)
        {
        }

        return result;
    }

    private static FormationEntry Entry(JsonElement element)
    {
        var id = element.TryGetProperty("id", out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : 0;
        var code = element.TryGetProperty("code", out var name) && name.ValueKind == JsonValueKind.String
            ? name.GetString() ?? string.Empty
            : string.Empty;
        var display = element.TryGetProperty("display", out var shown) && shown.ValueKind == JsonValueKind.String
            ? shown.GetString() ?? string.Empty
            : string.Empty;

        return new FormationEntry(id, code, display);
    }

    private static FormationCaptureOutcome Outcome(IReadOnlyList<FormationLayout> layouts,
        IReadOnlyList<string> unresolved)
    {
        FormationCaptureOutcome result;

        if (layouts.Count == 0) result = FormationCaptureOutcome.Failed;
        else if (unresolved.Count > 0) result = FormationCaptureOutcome.Partial;
        else result = FormationCaptureOutcome.Captured;

        return result;
    }

    private static string Detail(IReadOnlyList<FormationLayout> layouts, IReadOnlyList<string> unresolved)
    {
        var missed = unresolved.Count == 0
            ? string.Empty
            : $", slides {string.Join(", ", unresolved)} could not be resolved";

        return $"{layouts.Count} formations captured{missed}";
    }
}
