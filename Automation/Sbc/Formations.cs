namespace Automation.Sbc;

public static class Formations
{
    public const int DEFAULT_SQUAD_SIZE = 11;

    private static readonly IReadOnlyDictionary<string, string[]> Layouts =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["f442"] = ["GK", "RB", "CB", "CB", "LB", "RM", "CM", "CM", "LM", "ST", "ST"],
            ["f41212"] = ["GK", "RB", "CB", "CB", "LB", "CDM", "CM", "CM", "CAM", "ST", "ST"],
            ["f41212_2"] = ["GK", "RB", "CB", "CB", "LB", "CDM", "RM", "LM", "CAM", "ST", "ST"],
            ["f433"] = ["GK", "RB", "CB", "CB", "LB", "CM", "CM", "CM", "RW", "ST", "LW"],
            ["f4231"] = ["GK", "RB", "CB", "CB", "LB", "CDM", "CDM", "RM", "CAM", "LM", "ST"],
            ["f4231a"] = ["GK", "RB", "CB", "CB", "LB", "CDM", "CDM", "RW", "CAM", "LW", "ST"],
            ["f4141"] = ["GK", "RB", "CB", "CB", "LB", "CDM", "RM", "CM", "CM", "LM", "ST"],
            ["f4222"] = ["GK", "RB", "CB", "CB", "LB", "CDM", "CDM", "RM", "LM", "ST", "ST"],
            ["f4321"] = ["GK", "RB", "CB", "CB", "LB", "CM", "CM", "CM", "CAM", "CAM", "ST"],
            ["f451"] = ["GK", "RB", "CB", "CB", "LB", "RM", "CM", "CM", "CM", "LM", "ST"],
            ["f352"] = ["GK", "CB", "CB", "CB", "RM", "CM", "CM", "CM", "LM", "ST", "ST"],
            ["f343"] = ["GK", "CB", "CB", "CB", "RM", "CM", "CM", "LM", "RW", "ST", "LW"],
            ["f532"] = ["GK", "RWB", "CB", "CB", "CB", "LWB", "CM", "CM", "CM", "ST", "ST"],
            ["f541"] = ["GK", "RWB", "CB", "CB", "CB", "LWB", "RM", "CM", "CM", "LM", "ST"]
        };

    public static bool IsKnown(string formation)
    {
        return Layouts.ContainsKey(Normalise(formation));
    }

    public static IReadOnlyList<string> SlotPositions(string formation)
    {
        var key = Normalise(formation);
        IReadOnlyList<string> result = Layouts.TryGetValue(key, out var layout)
            ? layout
            : throw new ArgumentException($"unknown formation {formation}");

        return result;
    }

    private static string Normalise(string formation)
    {
        var trimmed = (formation ?? string.Empty).Trim();

        return trimmed.StartsWith('f') ? trimmed : $"f{trimmed}";
    }
}
