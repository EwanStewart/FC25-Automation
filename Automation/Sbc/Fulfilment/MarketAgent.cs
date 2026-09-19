using System.Globalization;
using Automation.Sbc;
using Automation.Sbc.Fulfilment;
using Automation.Setup;
using Automation.Trading;
using OpenQA.Selenium;
using static Automation.Definitions.Fc25Definitions;

namespace Automation;

public sealed record MarketPorts(
    Action<Filter> Search,
    Func<IWebElement, uint, string?, BidAttempt> Place,
    Action OpenTransferTargets,
    Action<int> Pause);

public sealed class MarketAgent : IMarketAgent
{
    private const int RESPONSE_POLL_MS = 250;
    private const int SETTLE_MS = 800;
    private const string WATCH_CONTROL = "Watch";
    private const string CLUB_CONTROL = "Send to My Club";
    private const string BUY_CONTROL = "Buy Now";
    private const string CONFIRM_TITLE = "Get Now";

    private static readonly TimeSpan ResponseWait = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ShortWait = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan DialogWait = TimeSpan.FromSeconds(6);

    private readonly Screen screen_;
    private readonly NetworkObserver network_;
    private readonly MarketPorts ports_;
    private readonly MarketPinner pinner_;

    public MarketAgent(Screen screen, NetworkObserver network, MarketPorts ports, MarketPinner? pinner = null)
    {
        screen_ = screen;
        network_ = network;
        ports_ = ports;
        pinner_ = pinner ?? MarketPinner.Ea;
    }

    public MarketSearch Search(MarketSpecification specification, uint ceiling)
    {
        var since = DateTime.UtcNow;
        var pins = pinner_.For(specification);

        ports_.Search(MarketFilter.For(specification, ceiling, pins));
        ports_.Pause(SETTLE_MS);

        var body = Settled(since);
        var rows = screen_.Snapshot(ElementKeys.RESULT_ROWS, RowModels.None).Count;

        return new MarketSearch(MarketFilter.Describe(specification, ceiling, pins),
            MarketCandidateChoice.Shown(UtasPayloads.ParseAuctions(body), rows));
    }

    public BidReceipt Bid(MarketChoice choice, uint amount)
    {
        return Placed(ElementKeys.RESULT_ROWS, choice.Listing.TradeId, amount, "results");
    }

    public BidReceipt BidOnTarget(TradeState target, uint amount)
    {
        return Placed(ElementKeys.TARGET_ROWS, target.TradeId, amount, "transfer targets");
    }

    public BuyReceipt BuyNow(AuctionListing listing, uint ceiling)
    {
        ForbiddenControls.Require(BUY_CONTROL);

        var row = Row(ElementKeys.RESULT_ROWS, listing.TradeId);
        BuyReceipt result;

        if (row is null) result = Declined($"the row for trade {listing.TradeId} left the results");
        else if (!screen_.Click(row, ShortWait))
            result = Declined($"the row for trade {listing.TradeId} would not open");
        else result = Offered(listing, ceiling);

        return result;
    }

    private BuyReceipt Offered(AuctionListing listing, uint ceiling)
    {
        screen_.DismissDialog();

        var opened = screen_.SelectedTrade();

        return opened == listing.TradeId
            ? Shown(listing, ceiling)
            : Declined($"the panel opened trade {Named(opened)} rather than {listing.TradeId}");
    }

    private static string Named(string tradeId)
    {
        return tradeId.Length > 0 ? tradeId : "nothing";
    }

    private BuyReceipt Shown(AuctionListing listing, uint ceiling)
    {
        var button = screen_.WaitEnabled(ElementKeys.BUY_NOW, ShortWait);
        BuyReceipt result;

        if (button is null) result = Declined($"no buy now control came up for trade {listing.TradeId}");
        else
        {
            var asked = Asked(button.Text);

            result = asked > 0 && asked <= ceiling && asked == listing.BuyNowPrice
                ? Pressed(button, listing.TradeId, asked)
                : Declined($"the panel asked {asked} for trade {listing.TradeId}, not the {ceiling} " +
                           $"this gap allows at the {listing.BuyNowPrice} the search read");
        }

        return result;
    }

    private BuyReceipt Pressed(IWebElement button, string tradeId, uint asked)
    {
        var since = DateTime.UtcNow;

        return screen_.Click(button, ShortWait)
            ? Asking(since, tradeId, asked)
            : Declined($"the buy now control for trade {tradeId} would not take a click");
    }

    private BuyReceipt Asking(DateTime since, string tradeId, uint asked)
    {
        var title = screen_.ReadText(ElementKeys.BUY_CONFIRM_TITLE, DialogWait).Trim();
        var message = screen_.ReadText(ElementKeys.BUY_CONFIRM_MESSAGE, ShortWait);

        return title == CONFIRM_TITLE && message.Contains(Money(asked), StringComparison.Ordinal)
            ? Taken(since, tradeId, asked)
            : Withdrawn(title, message, asked);
    }

    private BuyReceipt Withdrawn(string title, string message, uint asked)
    {
        screen_.Click(ElementKeys.BUY_CANCEL, TimeSpan.Zero);

        return Declined($"the app answered '{title}: {message}' rather than asking for {Money(asked)}");
    }

    private BuyReceipt Taken(DateTime since, string tradeId, uint asked)
    {
        return screen_.Click(ElementKeys.BUY_CONFIRM, ShortWait)
            ? Answered(since, tradeId, asked)
            : Declined($"the confirmation for trade {tradeId} would not take a click");
    }

    private BuyReceipt Answered(DateTime since, string tradeId, uint asked)
    {
        var deadline = DateTime.UtcNow + ResponseWait;
        var answer = Purchase(since, tradeId);

        while (answer is null && DateTime.UtcNow < deadline)
        {
            ports_.Pause(RESPONSE_POLL_MS);
            answer = Purchase(since, tradeId);
        }

        return answer is null ? Unanswered(tradeId, asked) : new BuyReceipt(answer.Bought,
            answer.Bought ? asked : 0, $"server {answer.Reason}");
    }

    private BuyResponse? Purchase(DateTime since, string tradeId)
    {
        return network_.Since(since, CaptureKind.Bid)
            .Select(capture => UtasPayloads.BuyResult(capture.Status, capture.Body, tradeId))
            .FirstOrDefault(answer => answer is not null);
    }

    private BuyReceipt Unanswered(string tradeId, uint asked)
    {
        var model = screen_.Snapshot(ElementKeys.RESULT_ROWS, RowModels.Results)
            .Select(row => row.TrustedModel).FirstOrDefault(row => row?.TradeId == tradeId);

        return model is not null && BidStates.Held(model.BidState ?? string.Empty)
            ? new BuyReceipt(true, asked, $"no answer was captured, but the row for trade {tradeId} reads as ours")
            : Declined($"no answer came back for the purchase of trade {tradeId}");
    }

    private static uint Asked(string label)
    {
        var digits = new string(label.Where(char.IsDigit).ToArray());

        return digits.Length > 0 && uint.TryParse(digits, out var parsed) ? parsed : 0;
    }

    private static string Money(uint amount)
    {
        return amount.ToString("N0", CultureInfo.InvariantCulture);
    }

    private static BuyReceipt Declined(string detail)
    {
        return new BuyReceipt(false, 0, detail);
    }

    public int Watch(IReadOnlyList<AuctionListing> listings)
    {
        ForbiddenControls.Require(WATCH_CONTROL);

        var watched = 0;

        foreach (var listing in listings)
            if (Watched(listing.TradeId)) watched++;

        return watched;
    }

    public bool Claim(TradeState target)
    {
        ForbiddenControls.Require(CLUB_CONTROL);

        return Pressed(ElementKeys.TARGET_ROWS, target.TradeId, ElementKeys.SEND_TO_CLUB);
    }

    public void Pause(int milliseconds)
    {
        ports_.Pause(milliseconds);
    }

    private bool Watched(string tradeId)
    {
        return Pressed(ElementKeys.RESULT_ROWS, tradeId, ElementKeys.WATCH);
    }

    private bool Pressed(ElementKeys rows, string tradeId, ElementKeys control)
    {
        var row = Row(rows, tradeId);
        var pressed = row is not null && screen_.Click(row, ShortWait) && Showing(tradeId) &&
                      screen_.Click(control, ShortWait);

        ports_.Pause(SETTLE_MS);
        screen_.DismissDialog();

        return pressed;
    }

    private bool Showing(string tradeId)
    {
        var opened = screen_.SelectedTrade();
        var showing = opened == tradeId;

        if (!showing) Console.WriteLine($"The panel opened trade {Named(opened)} rather than {tradeId}.");

        return showing;
    }

    private BidReceipt Placed(ElementKeys rows, string tradeId, uint amount, string where)
    {
        var row = Row(rows, tradeId);
        BidReceipt result;

        if (row is null) result = Withheld($"the row for trade {tradeId} left the {where}");
        else if (!screen_.Click(row, ShortWait)) result = Withheld($"the row for trade {tradeId} would not open");
        else result = Opened(tradeId, amount);

        screen_.DismissDialog();

        return result;
    }

    private BidReceipt Opened(string tradeId, uint amount)
    {
        var opened = screen_.SelectedTrade();

        return opened == tradeId
            ? Selected(tradeId, amount)
            : Withheld($"the panel opened trade {Named(opened)} rather than {tradeId}");
    }

    public IReadOnlyList<TradeState> Standing()
    {
        ports_.OpenTransferTargets();
        ports_.Pause(SETTLE_MS);

        return Targets();
    }

    public IReadOnlyList<TradeState> Targets()
    {
        return screen_.Snapshot(ElementKeys.TARGET_ROWS, RowModels.Watched)
            .Select(TargetRowState.Of)
            .Where(state => state is not null).Select(state => state!).ToList();
    }

    private BidReceipt Selected(string tradeId, uint amount)
    {
        var input = screen_.WaitVisible(ElementKeys.FIND_ALL_PRICE_INPUTS, ShortWait);
        BidReceipt result;

        if (input is null) result = Withheld("the bid box never appeared");
        else
        {
            var minimum = Utility.Utility.CommaSeperatedNumberToUInt(input.GetAttribute("value") ?? "0");

            result = minimum > amount
                ? Withheld($"the minimum bid moved to {minimum}, above the {amount} this gap allows")
                : Sent(input, tradeId, minimum);
        }

        return result;
    }

    private BidReceipt Sent(IWebElement input, string tradeId, uint minimum)
    {
        ForbiddenControls.Require(Elements[ElementKeys.MAKE_BID].Item2);

        var attempt = ports_.Place(input, minimum, tradeId);

        return new BidReceipt(attempt.Outcome, minimum,
            $"{attempt.Reason}, typed {attempt.Typed}, clicked {attempt.Clicked}");
    }

    private IWebElement? Row(ElementKeys rows, string tradeId)
    {
        var models = rows == ElementKeys.RESULT_ROWS ? RowModels.Results : RowModels.Watched;
        var snapshot = screen_.Snapshot(rows, models);
        var elements = screen_.FindAll(rows);
        var index = snapshot.ToList().FindIndex(row => row.TrustedModel?.TradeId == tradeId);

        return index >= 0 && index < elements.Count ? elements[index] : null;
    }

    private string Settled(DateTime since)
    {
        var deadline = DateTime.UtcNow + ResponseWait;
        var body = Body(since);

        while (body.Length == 0 && DateTime.UtcNow < deadline)
        {
            ports_.Pause(RESPONSE_POLL_MS);
            body = Body(since);
        }

        return body;
    }

    private string Body(DateTime since)
    {
        return network_.Since(since, CaptureKind.Search).Select(capture => capture.Body)
            .LastOrDefault(text => text.Length > 0) ?? string.Empty;
    }

    private static BidReceipt Withheld(string detail)
    {
        return new BidReceipt(BidOutcome.Failed, 0, detail, false);
    }
}
