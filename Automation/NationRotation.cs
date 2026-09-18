namespace Automation.Trading;

public static class NationRotation
{
    public static readonly string[] NATIONS =
    {
        "England", "Germany", "France", "Spain", "Italy", "Brazil", "Argentina", "Netherlands", "Portugal", "Belgium"
    };

    public static string? NextExploratory(IReadOnlyList<string> ring, string? last, ISet<string> cooling)
    {
        var start = last == null ? 0 : (ring.ToList().IndexOf(last) + 1) % ring.Count;

        return Enumerable.Range(0, ring.Count)
            .Select(offset => ring[(start + offset) % ring.Count])
            .FirstOrDefault(nation => !cooling.Contains(nation));
    }

    public static string? BestPerformer(IEnumerable<SegmentRecord> records, ISet<string> cooling)
    {
        return records
            .Select(record => (nation: Segments.Nation(record.Segment), record))
            .Where(candidate => candidate.nation != null && candidate.record.Won > 0 && !cooling.Contains(candidate.nation))
            .OrderByDescending(candidate => candidate.record.Profit)
            .ThenByDescending(candidate => WinRate(candidate.record))
            .ThenBy(candidate => candidate.nation)
            .Select(candidate => candidate.nation)
            .FirstOrDefault();
    }

    public static IReadOnlyList<string> Choose(IEnumerable<SegmentRecord> records, IReadOnlyList<string> ring,
        string? lastExploratory, ISet<string> cooling)
    {
        var exploratory = NextExploratory(ring, lastExploratory, cooling);
        var best = BestPerformer(records, cooling);

        return new[] { best, exploratory }
            .Where(nation => nation != null)
            .Distinct()
            .Select(nation => nation!)
            .ToList();
    }

    private static double WinRate(SegmentRecord record)
    {
        return (double)record.Won / (record.Won + record.Lost);
    }
}
