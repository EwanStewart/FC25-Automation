using static Automation.Definitions.Fc25Definitions;

namespace Automation.Sbc.Fulfilment;

public static class MarketFilter
{
    private const uint NO_MINIMUM_BUY_NOW = 0;

    public static Filter For(MarketSpecification specification, uint ceiling, MarketPins pins)
    {
        return new Filter
        {
            Quality = specification.Quality.ToString(),
            Position = specification.Position,
            Nationality = pins.Nation,
            League = pins.League,
            Club = pins.Club,
            PinsOptional = true,
            MaxBidPrice = ceiling,
            MinBuyPrice = NO_MINIMUM_BUY_NOW
        };
    }

    public static string Describe(MarketSpecification specification, uint ceiling, MarketPins pins)
    {
        var wanted = $"{specification.Quality} {specification.Position} at or under {ceiling}";
        var pinned = pins.Describe();

        return pinned.Length == 0 ? wanted : $"{wanted}, {pinned}";
    }
}
