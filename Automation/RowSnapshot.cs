using System.Text.Json;
using Automation.Flow;

namespace Automation.Trading;

public sealed record ModelSnapshot(
    string? TradeId,
    int? SecondsLeft,
    string? BidState,
    string? TradeState,
    uint? CurrentBid,
    uint? StartingBid,
    string? Name);

public sealed record RowSnapshot(
    int Index,
    string Classes,
    string Name,
    string Rating,
    string Position,
    string Description,
    string ItemClasses,
    string Time,
    string Bid,
    string Start,
    string BuyNow,
    ModelSnapshot? Model)
{
    public string Key => ItemKey.Build(Name, Description, ItemClasses, Rating, Position);

    public uint? MinutesLeft => Pricing.ParseMinutesRemaining(Time);

    public uint? BidValue => ParseCoins(Bid);

    public uint? StartValue => ParseCoins(Start);

    public uint? BuyNowValue => ParseCoins(BuyNow);

    public uint? NextBid => BidValue.HasValue ? BidValue.Value + Pricing.BidIncrement(BidValue.Value) : StartValue;

    public ModelSnapshot? TrustedModel =>
        Model != null && Model.StartingBid.HasValue && Model.StartingBid == StartValue ? Model : null;

    private static uint? ParseCoins(string text)
    {
        uint? result = null;

        if (uint.TryParse(text.Replace(",", ""), out var parsed)) result = parsed;

        return result;
    }
}

public static class RowSnapshotParser
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static IReadOnlyList<RowSnapshot> Parse(string json)
    {
        IReadOnlyList<RowSnapshot> result = [];

        try
        {
            result = JsonSerializer.Deserialize<List<RowSnapshot>>(json, Options) ?? [];
        }
        catch (JsonException)
        {
        }

        return result;
    }
}
