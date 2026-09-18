using Automation.Trading;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using static Automation.Definitions.Fc25Definitions;

namespace Automation.Setup;

public class Screen
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(300);

    private const string SnapshotScript = """
        const xpath = arguments[0];
        const withModel = arguments[1];
        const found = document.evaluate(xpath, document, null, XPathResult.ORDERED_NODE_SNAPSHOT_TYPE, null);
        const rows = [];
        for (let i = 0; i < found.snapshotLength; i++) rows.push(found.snapshotItem(i));
        let models = [];
        try {
            if (withModel && window.repositories && repositories.Item && repositories.Item.getWatchedItems) {
                models = repositories.Item.getWatchedItems().map(item => {
                    const auction = item._auction;
                    return auction ? {
                        tradeId: String(auction.tradeId), secondsLeft: auction.getSecondsRemaining(),
                        bidState: auction.bidState, tradeState: auction.tradeState,
                        currentBid: auction.currentBid, startingBid: auction.startingBid,
                        name: item._staticData ? item._staticData.name : null, rating: item.rating, used: false } : null;
                }).filter(model => model !== null);
            }
        } catch (error) { models = []; }
        const coins = text => { const parsed = parseInt(String(text).replace(/,/g, ''), 10); return isNaN(parsed) ? null : parsed; };
        const matchModel = (name, rating, start) => {
            const match = models.find(model => !model.used && model.name === name && String(model.rating) === rating && model.startingBid === start);
            if (match) match.used = true;
            return match ? { tradeId: match.tradeId, secondsLeft: match.secondsLeft, bidState: match.bidState, tradeState: match.tradeState,
                currentBid: match.currentBid, startingBid: match.startingBid, name: match.name } : null;
        };
        const text = (li, selector) => { const element = li.querySelector(selector); return element ? element.innerText.trim() : ''; };
        const value = (li, label) => {
            const match = Array.from(li.querySelectorAll('span.label')).find(element => element.innerText.trim() === label);
            return match && match.nextElementSibling ? match.nextElementSibling.innerText.trim() : '';
        };
        return JSON.stringify(rows.map((li, index) => {
            const item = li.querySelector('div.entityContainer > div.item');
            return { index: index, classes: li.className, name: text(li, 'div.entityContainer > div.name'),
                rating: text(li, 'div.rating'), position: text(li, 'div.position'), description: text(li, 'div.itemDesc'),
                itemClasses: item ? item.className : '', time: text(li, 'div.auction-state span.time'),
                bid: value(li, 'Bid'), start: value(li, 'Start Price:'), buyNow: value(li, 'Buy Now:'),
                model: matchModel(text(li, 'div.entityContainer > div.name'), text(li, 'div.rating'), coins(value(li, 'Start Price:'))) };
        }));
        """;

    private static readonly string[] DismissLabels = { "Cancel", "Close", "Ok", "Continue" };

    private readonly ChromeDriver _driver;

    public Screen(ChromeDriver driver)
    {
        _driver = driver;
    }

    public static By Locator(string selector)
    {
        return selector.StartsWith('/') || selector.StartsWith('(') ? By.XPath(selector) : By.CssSelector(selector);
    }

    public IList<IWebElement> FindAll(ElementKeys key)
    {
        return _driver.FindElements(Locator(Elements[key].Item1));
    }

    public IReadOnlyList<RowSnapshot> Snapshot(ElementKeys key, bool withModel)
    {
        var json = _driver.ExecuteScript(SnapshotScript, Elements[key].Item1, withModel) as string ?? string.Empty;

        return RowSnapshotParser.Parse(json);
    }

    public bool IsVisible(ElementKeys key)
    {
        return IsVisible(Locator(Elements[key].Item1));
    }

    public bool IsVisible(By locator)
    {
        var result = false;

        try
        {
            result = _driver.FindElements(locator).Any(element => element.Displayed);
        }
        catch (StaleElementReferenceException)
        {
        }

        return result;
    }

    public IWebElement? WaitVisible(ElementKeys key, TimeSpan timeout)
    {
        return WaitVisible(Locator(Elements[key].Item1), timeout);
    }

    public IWebElement? WaitVisible(By locator, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        IWebElement? result = FirstVisible(locator);

        while (result == null && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(PollInterval);
            result = FirstVisible(locator);
        }

        return result;
    }

    public IWebElement? WaitEnabled(ElementKeys key, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        IWebElement? result = FirstEnabled(key);

        while (result == null && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(PollInterval);
            result = FirstEnabled(key);
        }

        return result;
    }

    public bool WaitHidden(ElementKeys key, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        var hidden = !IsVisible(key);

        while (!hidden && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(PollInterval);
            hidden = !IsVisible(key);
        }

        return hidden;
    }

    private IWebElement? FirstEnabled(ElementKeys key)
    {
        IWebElement? result = null;

        try
        {
            result = FindAll(key).FirstOrDefault(element => element.Displayed && element.Enabled);
        }
        catch (StaleElementReferenceException)
        {
        }

        return result;
    }

    private IWebElement? FirstVisible(By locator)
    {
        IWebElement? result = null;

        try
        {
            result = _driver.FindElements(locator).FirstOrDefault(element => element.Displayed);
        }
        catch (StaleElementReferenceException)
        {
        }

        return result;
    }

    public string ReadText(ElementKeys key, TimeSpan timeout)
    {
        var element = WaitVisible(key, timeout);

        return element?.Text ?? string.Empty;
    }

    public string ReadTitle()
    {
        var title = FirstVisible(Locator(Elements[ElementKeys.SCREEN_TITLE].Item1));

        return title?.Text.Trim() ?? string.Empty;
    }

    public bool WaitForTitle(string title, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        var matched = ReadTitle() == title;

        while (!matched && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(PollInterval);
            matched = ReadTitle() == title;
        }

        return matched;
    }

    public bool IsShieldShowing()
    {
        return IsVisible(ElementKeys.CLICK_SHIELD);
    }

    public bool DismissDialog()
    {
        var dismissed = false;
        var buttons = VisibleDialogButtons();
        var button = buttons.FirstOrDefault(candidate => DismissLabels.Contains(candidate.Text.Trim(),
            StringComparer.OrdinalIgnoreCase));

        if (button != null)
        {
            Console.WriteLine($"Dismissing dialog with '{button.Text.Trim()}': {ReadDialogText()}");
            button.Click();
            dismissed = true;
        }

        return dismissed;
    }

    private List<IWebElement> VisibleDialogButtons()
    {
        List<IWebElement> result = [];

        try
        {
            result = FindAll(ElementKeys.DIALOG_BUTTONS).Where(button => button.Displayed).ToList();
        }
        catch (StaleElementReferenceException)
        {
        }

        return result;
    }

    private string ReadDialogText()
    {
        var dialog = FirstVisible(Locator(Elements[ElementKeys.DIALOG].Item1));

        return dialog?.Text.Replace('\n', ' ') ?? string.Empty;
    }

    public bool Click(ElementKeys key, TimeSpan timeout)
    {
        var clicked = TryClick(key, timeout);

        if (!clicked) Console.WriteLine($"Element '{Elements[key].Item2}' was not clicked.");

        return clicked;
    }

    public bool TryClick(ElementKeys key, TimeSpan timeout)
    {
        return Click(Locator(Elements[key].Item1), timeout);
    }

    public bool Click(By locator, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        var clicked = TryClickOnce(locator);

        while (!clicked && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(PollInterval);
            DismissDialog();
            clicked = TryClickOnce(locator);
        }

        return clicked;
    }

    public bool Click(IWebElement element, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        var outcome = TryClickOnce(element);

        while (outcome == ClickOutcome.Blocked && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(PollInterval);
            DismissDialog();
            outcome = TryClickOnce(element);
        }

        return outcome == ClickOutcome.Clicked;
    }

    private bool TryClickOnce(By locator)
    {
        var clicked = false;

        try
        {
            var element = _driver.FindElements(locator).FirstOrDefault(candidate => candidate.Displayed);
            if (element != null) clicked = TryClickOnce(element) == ClickOutcome.Clicked;
        }
        catch (StaleElementReferenceException)
        {
        }

        return clicked;
    }

    private ClickOutcome TryClickOnce(IWebElement element)
    {
        var outcome = ClickOutcome.Blocked;

        try
        {
            if (!IsShieldShowing() && element.Displayed && element.Enabled)
            {
                element.Click();
                outcome = ClickOutcome.Clicked;
            }
        }
        catch (ElementClickInterceptedException)
        {
        }
        catch (ElementNotInteractableException)
        {
        }
        catch (StaleElementReferenceException)
        {
            outcome = ClickOutcome.Gone;
        }

        return outcome;
    }

    public uint? SetInputValue(ElementKeys key, uint value, TimeSpan timeout)
    {
        var input = WaitVisible(key, timeout);
        uint? result = null;

        if (input != null) result = SetInputValue(input, value);
        else Console.WriteLine($"Input '{Elements[key].Item2}' was not found.");

        return result;
    }

    public uint? SetInputValue(IWebElement input, uint value)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        uint? result = TrySetInputValue(input, value);

        while (result == null && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(PollInterval);
            DismissDialog();
            result = TrySetInputValue(input, value);
        }

        if (result == null) Console.WriteLine($"Could not set input to {value}.");

        return result;
    }

    private uint? TrySetInputValue(IWebElement input, uint value)
    {
        uint? result = null;

        try
        {
            if (!IsShieldShowing())
            {
                input.Click();
                input.SendKeys(Keys.Control + "a");
                input.SendKeys(Keys.Backspace);
                input.SendKeys(value.ToString());
                Thread.Sleep(300);
                result = Utility.Utility.CommaSeperatedNumberToUInt(input.GetAttribute("value") ?? "0");
            }
        }
        catch (ElementClickInterceptedException)
        {
        }
        catch (ElementNotInteractableException)
        {
        }
        catch (FormatException)
        {
        }

        return result;
    }

    private enum ClickOutcome
    {
        Clicked,
        Blocked,
        Gone
    }
}
