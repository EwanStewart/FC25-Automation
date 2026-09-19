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

    private static readonly TimeSpan ResponseWait = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ShortWait = TimeSpan.FromSeconds(3);

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
        var pressed = row is not null && screen_.Click(row, ShortWait) &&
                      screen_.Click(control, ShortWait);

        ports_.Pause(SETTLE_MS);
        screen_.DismissDialog();

        return pressed;
    }

    private BidReceipt Placed(ElementKeys rows, string tradeId, uint amount, string where)
    {
        var row = Row(rows, tradeId);
        BidReceipt result;

        if (row is null) result = Withheld($"the row for trade {tradeId} left the {where}");
        else if (!screen_.Click(row, ShortWait)) result = Withheld($"the row for trade {tradeId} would not open");
        else result = Selected(tradeId, amount);

        screen_.DismissDialog();

        return result;
    }

    public IReadOnlyList<TradeState> Standing()
    {
        ports_.OpenTransferTargets();
        ports_.Pause(SETTLE_MS);

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
