using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using static Automation.Definitions.Fc25Definitions;

namespace Automation.Setup;

public class Screen
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(300);

    private static readonly string[] DismissLabels = { "Cancel", "Close", "Ok" };

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

    public void Hide(IReadOnlyCollection<IWebElement> elements)
    {
        if (elements.Count > 0)
            _driver.ExecuteScript(
                "for (const element of arguments[0]) { element.classList.add('bot-skip'); element.style.display = 'none'; }",
                elements);
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
            Console.WriteLine($"Dismissing dialog: {ReadDialogText()}");
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
