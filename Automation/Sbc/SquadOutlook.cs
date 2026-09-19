namespace Automation.Sbc;

public sealed record GapPossibility(
    int SlotIndex,
    int? ClubId,
    int? LeagueId,
    int? NationId,
    int? RareFlag,
    int MinimumRating,
    int MaximumRating);

public sealed record RequirementRange(int Minimum, int Maximum);

public static class SquadOutlook
{
    private const int NOVEL_BASE = 800001;
    private const int NOVEL_STRIDE = 3;

    public static IReadOnlyList<SquadRequirement> Breaches(ChallengeRequirements challenge,
        IReadOnlyList<SquadPlayer> squad, IReadOnlyList<GapPossibility> gaps, ChemistryThresholds thresholds,
        TeamLinks links)
    {
        var outlook = Of(challenge, squad, gaps, thresholds, links);

        return challenge.Requirements.Where(Modelled).Where(requirement => !Holds(requirement, outlook)).ToList();
    }

    private static bool Modelled(SquadRequirement requirement)
    {
        return requirement.Kind != RequirementKind.Unsupported;
    }

    private static Outlook Of(ChallengeRequirements challenge, IReadOnlyList<SquadPlayer> squad,
        IReadOnlyList<GapPossibility> gaps, ChemistryThresholds thresholds, TeamLinks links)
    {
        var keyed = gaps.ToDictionary(gap => gap.SlotIndex);

        return new Outlook(challenge, squad, keyed,
            squad.Where((_, index) => !keyed.ContainsKey(index)).ToList(),
            Ratings(squad, keyed), Chemistry(challenge, Scattered(squad, keyed), thresholds, links),
            keyed.Values.Any(Loose));
    }

    private static bool Loose(GapPossibility gap)
    {
        return !gap.ClubId.HasValue || !gap.LeagueId.HasValue || !gap.NationId.HasValue;
    }

    private static ChemistryResult Chemistry(ChallengeRequirements challenge, IReadOnlyList<SquadPlayer> squad,
        ChemistryThresholds thresholds, TeamLinks links)
    {
        return ChemistryCalculator.Calculate(Formations.SlotPositions(challenge.Formation),
            squad.Select(player => (ChemistryPlayer?)player.ToChemistryPlayer()).ToList(), null, thresholds, links);
    }

    private static IReadOnlyList<SquadPlayer> Scattered(IReadOnlyList<SquadPlayer> squad,
        IReadOnlyDictionary<int, GapPossibility> gaps)
    {
        return squad.Select((player, index) =>
            gaps.TryGetValue(index, out var gap) ? Novel(player, gap, index) : player).ToList();
    }

    private static SquadPlayer Novel(SquadPlayer player, GapPossibility gap, int index)
    {
        return player with
        {
            TeamId = gap.ClubId ?? NOVEL_BASE + index * NOVEL_STRIDE,
            LeagueId = gap.LeagueId ?? NOVEL_BASE + index * NOVEL_STRIDE + 1,
            NationId = gap.NationId ?? NOVEL_BASE + index * NOVEL_STRIDE + 2
        };
    }

    private static RequirementRange Ratings(IReadOnlyList<SquadPlayer> squad,
        IReadOnlyDictionary<int, GapPossibility> gaps)
    {
        return new RequirementRange(SquadAssessor.Rating(Rated(squad, gaps, gap => gap.MinimumRating)),
            SquadAssessor.Rating(Rated(squad, gaps, gap => gap.MaximumRating)));
    }

    private static IReadOnlyList<SquadPlayer> Rated(IReadOnlyList<SquadPlayer> squad,
        IReadOnlyDictionary<int, GapPossibility> gaps, Func<GapPossibility, int> edge)
    {
        return squad.Select((player, index) =>
            gaps.TryGetValue(index, out var gap) ? player with { Rating = edge(gap) } : player).ToList();
    }

    private static bool Holds(SquadRequirement requirement, Outlook outlook)
    {
        return requirement.Kind == RequirementKind.EveryPlayer
            ? Everyone(requirement, outlook)
            : Fits(Range(requirement, outlook), requirement);
    }

    private static bool Everyone(SquadRequirement requirement, Outlook outlook)
    {
        return outlook.Settled.All(player =>
                   SquadAssessor.Matches(player, requirement.Filters, requirement.Comparison)) &&
               outlook.Gaps.Values.All(gap => Fit(requirement, outlook, gap, requirement.Comparison).Certain);
    }

    private static bool Fits(RequirementRange range, SquadRequirement requirement)
    {
        return requirement.Comparison switch
        {
            RequirementComparison.Maximum => range.Maximum <= requirement.Value,
            RequirementComparison.Exact => range.Minimum == requirement.Value &&
                                           range.Maximum == requirement.Value,
            _ => range.Minimum >= requirement.Value
        };
    }

    private static RequirementRange Range(SquadRequirement requirement, Outlook outlook)
    {
        return requirement.Kind switch
        {
            RequirementKind.PlayerCount or RequirementKind.PlayerLevelCount => Counted(requirement, outlook),
            RequirementKind.SquadRating or RequirementKind.StarRating => outlook.Ratings,
            RequirementKind.TotalChemistry => Banded(outlook, outlook.Chemistry.Total,
                ChemistryCalculator.SLOT_MAX_CHEMISTRY * outlook.Squad.Count),
            RequirementKind.PlayerChemistry => Banded(outlook, Weakest(outlook),
                ChemistryCalculator.SLOT_MAX_CHEMISTRY),
            RequirementKind.DistinctClubs => Spread(outlook, Club, gap => gap.ClubId),
            RequirementKind.DistinctLeagues => Spread(outlook, League, gap => gap.LeagueId),
            RequirementKind.DistinctNations => Spread(outlook, Nation, gap => gap.NationId),
            RequirementKind.SameClubCount => Biggest(outlook, Club, gap => gap.ClubId),
            RequirementKind.SameLeagueCount => Biggest(outlook, League, gap => gap.LeagueId),
            RequirementKind.SameNationCount => Biggest(outlook, Nation, gap => gap.NationId),
            RequirementKind.LegendCount => Legends(outlook),
            _ => new RequirementRange(0, 0)
        };
    }

    private static int Club(SquadPlayer player)
    {
        return player.TeamId;
    }

    private static int League(SquadPlayer player)
    {
        return player.LeagueId;
    }

    private static int Nation(SquadPlayer player)
    {
        return player.NationId;
    }

    private static int Weakest(Outlook outlook)
    {
        return outlook.Chemistry.SlotPoints.Count == 0 ? 0 : outlook.Chemistry.SlotPoints.Min();
    }

    private static RequirementRange Banded(Outlook outlook, int floor, int ceiling)
    {
        return new RequirementRange(floor, outlook.AnythingFree ? ceiling : floor);
    }

    private static RequirementRange Counted(SquadRequirement requirement, Outlook outlook)
    {
        var settled = outlook.Settled.Count(player =>
            SquadAssessor.Matches(player, requirement.Filters, RequirementComparison.Exact));
        var fits = outlook.Gaps.Values
            .Select(gap => Fit(requirement, outlook, gap, RequirementComparison.Exact)).ToList();

        return new RequirementRange(settled + fits.Count(fit => fit.Certain),
            settled + fits.Count(fit => fit.Possible));
    }

    private static FilterChance Fit(SquadRequirement requirement, Outlook outlook, GapPossibility gap,
        RequirementComparison comparison)
    {
        var chances = requirement.Filters
            .Select(filter => Chance(filter, gap, outlook.Squad[gap.SlotIndex], comparison)).ToList();

        return new FilterChance(chances.All(chance => chance.Certain), chances.All(chance => chance.Possible));
    }

    private static FilterChance Chance(PlayerFilter filter, GapPossibility gap, SquadPlayer player,
        RequirementComparison comparison)
    {
        return filter.Kind switch
        {
            PlayerFilterKind.Quality => Qualities(gap,
                quality => SquadAssessor.Compare((int)quality, filter.Value, comparison)),
            PlayerFilterKind.Level => Qualities(gap, quality => (int)quality == filter.Value),
            PlayerFilterKind.Rarity => Optional(gap.RareFlag, filter),
            PlayerFilterKind.Nation => Optional(gap.NationId, filter),
            PlayerFilterKind.League => Optional(gap.LeagueId, filter),
            PlayerFilterKind.Club => Optional(gap.ClubId, filter),
            PlayerFilterKind.MinimumRating => new FilterChance(gap.MinimumRating >= filter.Value,
                gap.MaximumRating >= filter.Value),
            PlayerFilterKind.MaximumRating => new FilterChance(gap.MaximumRating <= filter.Value,
                gap.MinimumRating <= filter.Value),
            PlayerFilterKind.ExactRating => new FilterChance(
                gap.MinimumRating == filter.Value && gap.MaximumRating == filter.Value,
                filter.Value >= gap.MinimumRating && filter.Value <= gap.MaximumRating),
            _ => Certain(SquadAssessor.Matches(player, [filter], comparison))
        };
    }

    private static FilterChance Qualities(GapPossibility gap, Func<PlayerQuality, bool> accepts)
    {
        var bands = Enumerable.Range(gap.MinimumRating, Math.Max(1, gap.MaximumRating - gap.MinimumRating + 1))
            .Select(QualityBand.Of).Distinct().ToList();

        return new FilterChance(bands.All(accepts), bands.Any(accepts));
    }

    private static FilterChance Optional(int? value, PlayerFilter filter)
    {
        return value.HasValue ? Certain(filter.Accepts(value.Value)) : new FilterChance(false, true);
    }

    private static FilterChance Certain(bool matched)
    {
        return new FilterChance(matched, matched);
    }

    private static RequirementRange Spread(Outlook outlook, Func<SquadPlayer, int> key,
        Func<GapPossibility, int?> pin)
    {
        var known = Known(outlook, key, pin);
        var free = Free(outlook, pin);
        var distinct = known.Distinct().Count();

        return new RequirementRange(distinct > 0 ? distinct : Math.Min(free, 1), distinct + free);
    }

    private static RequirementRange Biggest(Outlook outlook, Func<SquadPlayer, int> key,
        Func<GapPossibility, int?> pin)
    {
        var known = Known(outlook, key, pin);
        var free = Free(outlook, pin);
        var largest = known.GroupBy(value => value).Select(group => group.Count()).DefaultIfEmpty(0).Max();

        return new RequirementRange(largest > 0 ? largest : Math.Min(free, 1), largest + free);
    }

    private static RequirementRange Legends(Outlook outlook)
    {
        var settled = outlook.Settled.Count(IsLegend);
        var certain = outlook.Gaps.Values.Count(gap => gap.ClubId == ChemistryCalculator.LEGENDS_CLUB_ID ||
                                                       gap.LeagueId == ChemistryCalculator.LEGENDS_LEAGUE_ID);
        var possible = outlook.Gaps.Values.Count(gap =>
            !gap.ClubId.HasValue || !gap.LeagueId.HasValue ||
            gap.ClubId == ChemistryCalculator.LEGENDS_CLUB_ID ||
            gap.LeagueId == ChemistryCalculator.LEGENDS_LEAGUE_ID);

        return new RequirementRange(settled + certain, settled + possible);
    }

    private static bool IsLegend(SquadPlayer player)
    {
        return player.TeamId == ChemistryCalculator.LEGENDS_CLUB_ID ||
               player.LeagueId == ChemistryCalculator.LEGENDS_LEAGUE_ID;
    }

    private static IReadOnlyList<int> Known(Outlook outlook, Func<SquadPlayer, int> key,
        Func<GapPossibility, int?> pin)
    {
        return outlook.Settled.Select(key)
            .Concat(outlook.Gaps.Values.Select(pin).Where(value => value.HasValue).Select(value => value!.Value))
            .ToList();
    }

    private static int Free(Outlook outlook, Func<GapPossibility, int?> pin)
    {
        return outlook.Gaps.Values.Count(gap => !pin(gap).HasValue);
    }

    private sealed record FilterChance(bool Certain, bool Possible);

    private sealed record Outlook(
        ChallengeRequirements Challenge,
        IReadOnlyList<SquadPlayer> Squad,
        IReadOnlyDictionary<int, GapPossibility> Gaps,
        IReadOnlyList<SquadPlayer> Settled,
        RequirementRange Ratings,
        ChemistryResult Chemistry,
        bool AnythingFree);
}
