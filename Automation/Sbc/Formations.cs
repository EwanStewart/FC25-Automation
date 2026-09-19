using System.Reflection;

namespace Automation.Sbc;

public static class Formations
{
    public const int DEFAULT_SQUAD_SIZE = FormationLayouts.SQUAD_SIZE;

    private const string RESOURCE_NAME = "Automation.Sbc.formations.json";

    private static readonly Lazy<IReadOnlyDictionary<string, FormationLayout>> Captured = new(Load);

    public static IReadOnlyList<string> Codes => Captured.Value.Values.Select(layout => layout.Code).ToList();

    public static bool IsKnown(string formation)
    {
        return Captured.Value.ContainsKey(Normalise(formation));
    }

    public static IReadOnlyList<string> SlotPositions(string formation)
    {
        var key = Normalise(formation);
        IReadOnlyList<string> result = Captured.Value.TryGetValue(key, out var layout)
            ? layout.Slots
            : throw new ArgumentException($"unknown formation {formation}");

        return result;
    }

    public static string DisplayName(string formation)
    {
        var key = Normalise(formation);
        var result = Captured.Value.TryGetValue(key, out var layout)
            ? layout.DisplayName
            : throw new ArgumentException($"unknown formation {formation}");

        return result;
    }

    private static IReadOnlyDictionary<string, FormationLayout> Load()
    {
        return FormationLayouts.Parse(CapturedJson())
            .ToDictionary(layout => layout.Code, StringComparer.OrdinalIgnoreCase);
    }

    private static string CapturedJson()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(RESOURCE_NAME)
                           ?? throw new FormationLayoutException($"{RESOURCE_NAME} is not built into the assembly");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }

    private static string Normalise(string formation)
    {
        var trimmed = (formation ?? string.Empty).Trim();

        return trimmed.StartsWith('f') || trimmed.StartsWith('F') ? trimmed : $"f{trimmed}";
    }
}
