using System.Text.Json;
using Automation.Trading;

namespace Automation.Sbc.Fulfilment;

public static class SquadReader
{
    private const string EMPTY_STATE = "invalid";
    private const string CHALLENGE_PATH = "/sbs/challenge/";

    public static int ChallengeIn(string url)
    {
        var marker = url.IndexOf(CHALLENGE_PATH, StringComparison.Ordinal);
        var result = 0;

        if (marker >= 0)
            int.TryParse(new string(url[(marker + CHALLENGE_PATH.Length)..].TakeWhile(char.IsDigit).ToArray()),
                out result);

        return result;
    }

    public static SquadView? Read(string json)
    {
        SquadView? result = null;

        try
        {
            using var document = JsonDocument.Parse(json);

            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("squad", out var squad)) result = Build(document.RootElement,
                squad);
        }
        catch (JsonException)
        {
        }

        return result;
    }

    private static SquadView Build(JsonElement root, JsonElement squad)
    {
        return new SquadView(Number(root, "challengeId"), Text(squad, "formation"), Slots(squad));
    }

    private static IReadOnlyList<SquadSlotView> Slots(JsonElement squad)
    {
        return squad.TryGetProperty("players", out var players) && players.ValueKind == JsonValueKind.Array
            ? players.EnumerateArray().Select(Slot).OrderBy(slot => slot.Index).ToList()
            : [];
    }

    private static SquadSlotView Slot(JsonElement player)
    {
        var item = player.TryGetProperty("itemData", out var data) ? data : default;
        var id = Identity(item, "id");

        return new SquadSlotView(Number(player, "index"), Text(item, "preferredPosition"), id,
            Number(item, "assetId"), Number(item, "rating"), id > 0 && Text(item, "itemState") != EMPTY_STATE);
    }

    private static string Text(JsonElement element, string name)
    {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static int Number(JsonElement element, string name)
    {
        return (int)Math.Min(int.MaxValue, Identity(element, name));
    }

    private static long Identity(JsonElement element, string name)
    {
        long result = 0;

        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var parsed)) result = parsed;

        return result;
    }
}

public static class TargetRowState
{
    private const string WON_CLASS = "won";
    private const string EXPIRED_CLASS = "expired";
    private const string CLOSED_STATE = "closed";
    private const string HIGHEST_STATE = "highest";

    public static TradeState? Of(RowSnapshot row)
    {
        var model = row.TrustedModel;
        var tokens = row.Classes.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

        return model?.TradeId is null
            ? null
            : new TradeState(model.TradeId, State(tokens, model), BidState(tokens, model), row.BidValue ?? 0,
                model.SecondsLeft, row.NextBid ?? 0, model.ItemId ?? 0);
    }

    private static string State(IReadOnlySet<string> tokens, ModelSnapshot model)
    {
        var result = model.TradeState ?? string.Empty;

        if (tokens.Contains(WON_CLASS)) result = CLOSED_STATE;
        else if (tokens.Contains(EXPIRED_CLASS)) result = EXPIRED_CLASS;

        return result;
    }

    private static string BidState(IReadOnlySet<string> tokens, ModelSnapshot model)
    {
        return tokens.Contains(WON_CLASS) ? HIGHEST_STATE : model.BidState ?? string.Empty;
    }
}
