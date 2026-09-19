namespace Automation.Sbc;

public sealed record RequirementOutcome(SquadRequirement Requirement, bool Passed, int Actual);

public sealed record SquadAssessment(
    IReadOnlyList<RequirementOutcome> Outcomes,
    int Rating,
    ChemistryResult Chemistry)
{
    public bool IsValid => Outcomes.All(outcome => outcome.Passed);
}

public static class SquadAssessor
{
    public static SquadAssessment Assess(ChallengeRequirements challenge, IReadOnlyList<SquadPlayer> squad,
        SquadPlayer? manager, ChemistryThresholds thresholds, TeamLinks links)
    {
        var chemistry = ChemistryCalculator.Calculate(Formations.SlotPositions(challenge.Formation),
            squad.Select(player => (ChemistryPlayer?)player.ToChemistryPlayer()).ToList(),
            manager?.ToChemistryPlayer(), thresholds, links);
        var outcomes = challenge.Requirements
            .Select(requirement => Evaluate(requirement, squad, chemistry, Rating(squad))).ToList();

        return new SquadAssessment(outcomes, Rating(squad), chemistry);
    }

    public static int Rating(IReadOnlyList<SquadPlayer> squad)
    {
        var result = 0;

        if (squad.Count > 0)
        {
            var total = squad.Sum(player => player.Rating);
            var average = (double)total / squad.Count;
            var surplus = squad.Sum(player => Math.Max(0, player.Rating - average));
            result = (int)Math.Floor((total + surplus) / squad.Count);
        }

        return result;
    }

    private static RequirementOutcome Evaluate(SquadRequirement requirement, IReadOnlyList<SquadPlayer> squad,
        ChemistryResult chemistry, int rating)
    {
        var actual = Measure(requirement, squad, chemistry, rating);
        var passed = requirement.Kind != RequirementKind.Unsupported && Passes(requirement, squad, actual);

        return new RequirementOutcome(requirement, passed, actual);
    }

    private static bool Passes(SquadRequirement requirement, IReadOnlyList<SquadPlayer> squad, int actual)
    {
        return requirement.Kind == RequirementKind.EveryPlayer
            ? squad.All(player => Matches(player, requirement.Filters, requirement.Comparison))
            : Compare(actual, requirement.Value, requirement.Comparison);
    }

    private static int Measure(SquadRequirement requirement, IReadOnlyList<SquadPlayer> squad,
        ChemistryResult chemistry, int rating)
    {
        return requirement.Kind switch
        {
            RequirementKind.PlayerCount or RequirementKind.PlayerLevelCount => squad.Count(player =>
                Matches(player, requirement.Filters, RequirementComparison.Exact)),
            RequirementKind.EveryPlayer => squad.Count(player =>
                Matches(player, requirement.Filters, requirement.Comparison)),
            RequirementKind.SquadRating => rating,
            RequirementKind.StarRating => rating,
            RequirementKind.TotalChemistry => chemistry.Total,
            RequirementKind.PlayerChemistry => chemistry.SlotPoints.Count == 0 ? 0 : chemistry.SlotPoints.Min(),
            RequirementKind.DistinctNations => squad.Select(player => player.NationId).Distinct().Count(),
            RequirementKind.DistinctLeagues => squad.Select(player => player.LeagueId).Distinct().Count(),
            RequirementKind.DistinctClubs => squad.Select(player => player.TeamId).Distinct().Count(),
            RequirementKind.SameNationCount => Largest(squad.Select(player => player.NationId)),
            RequirementKind.SameLeagueCount => Largest(squad.Select(player => player.LeagueId)),
            RequirementKind.SameClubCount => Largest(squad.Select(player => player.TeamId)),
            RequirementKind.LegendCount => squad.Count(IsLegend),
            _ => 0
        };
    }

    private static bool IsLegend(SquadPlayer player)
    {
        return player.TeamId == ChemistryCalculator.LEGENDS_CLUB_ID ||
               player.LeagueId == ChemistryCalculator.LEGENDS_LEAGUE_ID;
    }

    private static int Largest(IEnumerable<int> values)
    {
        var groups = values.GroupBy(value => value).Select(group => group.Count()).ToList();

        return groups.Count == 0 ? 0 : groups.Max();
    }

    public static bool Matches(SquadPlayer player, IReadOnlyList<PlayerFilter> filters,
        RequirementComparison comparison)
    {
        return filters.All(filter => MatchesFilter(player, filter, comparison));
    }

    private static bool MatchesFilter(SquadPlayer player, PlayerFilter filter, RequirementComparison comparison)
    {
        return filter.Kind switch
        {
            PlayerFilterKind.Quality => Compare((int)QualityBand.Of(player.Rating), filter.Value, comparison),
            PlayerFilterKind.Level => (int)QualityBand.Of(player.Rating) == filter.Value,
            PlayerFilterKind.Rarity => filter.Accepts(player.RareFlag),
            PlayerFilterKind.Nation => filter.Accepts(player.NationId),
            PlayerFilterKind.League => filter.Accepts(player.LeagueId),
            PlayerFilterKind.Club => filter.Accepts(player.TeamId),
            PlayerFilterKind.MinimumRating => player.Rating >= filter.Value,
            PlayerFilterKind.MaximumRating => player.Rating <= filter.Value,
            PlayerFilterKind.ExactRating => player.Rating == filter.Value,
            _ => player.Untradeable == (filter.Value == 0)
        };
    }

    public static bool Compare(int actual, int expected, RequirementComparison comparison)
    {
        return comparison switch
        {
            RequirementComparison.Maximum => actual <= expected,
            RequirementComparison.Exact => actual == expected,
            _ => actual >= expected
        };
    }
}
