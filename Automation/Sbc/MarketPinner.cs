namespace Automation.Sbc;

public sealed record MarketPins(string? Nation, string? League, string? Club, IReadOnlyList<string> Notes)
{
    public bool Pinned => Nation is not null || League is not null || Club is not null;

    public string Describe()
    {
        List<string> parts = [];

        if (Nation is not null) parts.Add($"country {Nation}");
        if (League is not null) parts.Add($"league {League}");
        if (Club is not null) parts.Add($"club {Club}");
        parts.AddRange(Notes);

        return string.Join(", ", parts);
    }
}

public sealed class MarketPinner
{
    private static readonly Lazy<MarketPinner> SHIPPED = new(() => new MarketPinner(MarketNames.Ea,
        MarketReference.Ea));

    private readonly MarketNames names_;
    private readonly MarketReference reference_;

    public MarketPinner(MarketNames names, MarketReference reference)
    {
        names_ = names;
        reference_ = reference;
    }

    public static MarketPinner Ea => SHIPPED.Value;

    public MarketPins For(MarketSpecification specification)
    {
        List<string> notes = [];
        var nation = Named(specification.NationId, names_.Nation, "country", notes);
        var league = Named(LeagueFor(specification), names_.League, "league", notes);
        var club = ClubFor(specification, league, notes);

        return new MarketPins(nation, league, club, notes);
    }

    private static string? Named(int? id, Func<int, string?> lookup, string kind, List<string> notes)
    {
        string? result = null;

        if (id.HasValue)
        {
            result = lookup(id.Value);

            if (result is null) notes.Add($"{kind} {id.Value} has no name the search can pin");
        }

        return result;
    }

    private int? LeagueFor(MarketSpecification specification)
    {
        var implied = specification.ClubId.HasValue ? reference_.LeagueOf(specification.ClubId.Value) : 0;

        return specification.LeagueId ?? (implied > 0 ? implied : null);
    }

    private string? ClubFor(MarketSpecification specification, string? league, List<string> notes)
    {
        string? result = null;

        if (specification.ClubId.HasValue)
        {
            var named = names_.Club(specification.ClubId.Value);

            if (named is null) notes.Add($"club {specification.ClubId.Value} has no name the search can pin");
            else if (league is null && !names_.ClubNameIsUnique(specification.ClubId.Value))
                notes.Add($"club {named} is shared by more than one club and no league narrows it");
            else result = named;
        }

        return result;
    }
}
