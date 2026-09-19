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

        return new MarketSearch(MarketFilter.Describe(specification, ceiling, pins),
            UtasPayloads.ParseAuctions(body));
    }

    public BidReceipt Bid(MarketChoice choice, uint amount)
    {
        var row = Row(choice.Listing.TradeId);
        BidReceipt result;

        if (row is null) result = Withheld($"the row for trade {choice.Listing.TradeId} left the results");
        else if (!screen_.Click(row.Value.Element, ShortWait))
            result = Withheld($"the row for trade {choice.Listing.TradeId} would not open");
        else result = Selected(choice, amount);

        screen_.DismissDialog();

        return result;
    }

    public IReadOnlyList<TradeState> Standing()
    {
        ports_.OpenTransferTargets();
        ports_.Pause(SETTLE_MS);

        return screen_.Snapshot(ElementKeys.TARGET_ROWS, RowModels.Watched)
            .Select(row => TargetRowState.Of(row.Classes, row.TrustedModel, row.BidValue ?? 0))
            .Where(state => state is not null).Select(state => state!).ToList();
    }

    private BidReceipt Selected(MarketChoice choice, uint amount)
    {
        var input = screen_.WaitVisible(ElementKeys.FIND_ALL_PRICE_INPUTS, ShortWait);
        BidReceipt result;

        if (input is null) result = Withheld("the bid box never appeared");
        else
        {
            var minimum = Utility.Utility.CommaSeperatedNumberToUInt(input.GetAttribute("value") ?? "0");

            result = minimum > amount
                ? Withheld($"the minimum bid moved to {minimum}, above the {amount} this gap allows")
                : Sent(input, choice, minimum);
        }

        return result;
    }

    private BidReceipt Sent(IWebElement input, MarketChoice choice, uint minimum)
    {
        ForbiddenControls.Require(Elements[ElementKeys.MAKE_BID].Item2);

        var attempt = ports_.Place(input, minimum, choice.Listing.TradeId);

        return new BidReceipt(attempt.Outcome, minimum,
            $"{attempt.Reason}, typed {attempt.Typed}, clicked {attempt.Clicked}");
    }

    private (IWebElement Element, string TradeId)? Row(string tradeId)
    {
        var snapshot = screen_.Snapshot(ElementKeys.RESULT_ROWS, RowModels.Results);
        var elements = screen_.FindAll(ElementKeys.RESULT_ROWS);
        var index = snapshot.ToList().FindIndex(row => row.TrustedModel?.TradeId == tradeId);

        return index >= 0 && index < elements.Count ? (elements[index], tradeId) : null;
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
