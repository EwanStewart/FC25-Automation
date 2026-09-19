using System.Globalization;
using Automation.Setup;
using Automation.Trading;
using OpenQA.Selenium.Chrome;
using static Automation.Definitions.Fc25Definitions;

namespace Automation;

public sealed class ClubCapture
{
    private const string CentreScript = """
        const element = document.querySelector(arguments[0]);
        if (!element) return '';
        const rect = element.getBoundingClientRect();
        return (rect.left + rect.width / 2) + ',' + (rect.top + rect.height / 2);
        """;

    private const string CLUB_TITLE = "Club";
    private const string CLUB_PLAYERS_TITLE = "My Club Players";
    private const int RESPONSE_POLL_MS = 250;
    private const int SCREEN_SETTLE_MS = 1500;

    private static readonly TimeSpan ScreenWait = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ResponseWait = TimeSpan.FromSeconds(10);

    private readonly ChromeDriver _driver;
    private readonly Screen _screen;
    private readonly NetworkObserver _network;
    private readonly MouseInput _mouse;
    private readonly Action _reload;

    public ClubCapture(ChromeDriver driver, Screen screen, NetworkObserver network, MouseInput mouse, Action reload)
    {
        _driver = driver;
        _screen = screen;
        _network = network;
        _mouse = mouse;
        _reload = reload;
    }

    public ClubReading Capture()
    {
        var fault = ReadinessFault();
        var result = fault.Length > 0
            ? new ClubReading(ClubCaptureOutcome.NotObserved, [], fault)
            : CaptureThroughTheApp();

        Persist(result);
        Announce(result);

        return result;
    }

    private string ReadinessFault()
    {
        var result = string.Empty;

        if (!_network.Enabled) result = "the network observer is not running";
        else if (!_mouse.Enabled) result = "mouse input is not running";

        return result;
    }

    private ClubReading CaptureThroughTheApp()
    {
        var result = Attempt();

        if (result.Outcome == ClubCaptureOutcome.NotObserved)
        {
            Console.WriteLine("The club screen answered from its cache, reloading the web app for a fresh read.");
            _reload();
            result = Attempt();
        }

        return result;
    }

    private ClubReading Attempt()
    {
        var started = DateTime.UtcNow;

        EnterClubPlayers();
        WaitForClubResponse(started);

        return ClubInventory.Read(Responses(started));
    }

    private void EnterClubPlayers()
    {
        ClearBlockingDialog();

        var onHub = OpenScreen(ElementKeys.CLUB_TAB, CLUB_TITLE);
        var onPlayers = onHub && OpenScreen(ElementKeys.CLUB_PLAYERS_TILE, CLUB_PLAYERS_TITLE);

        Console.WriteLine($"Club hub reached {onHub}, club players reached {onPlayers}.");
    }

    private bool OpenScreen(ElementKeys key, string title)
    {
        var opened = ClickThrough(key, title);

        if (!opened) opened = ClickThrough(key, title);

        return opened;
    }

    private bool ClickThrough(ElementKeys key, string title)
    {
        _screen.WaitVisible(key, ScreenWait);
        Thread.Sleep(SCREEN_SETTLE_MS);

        return ClickByMouse(Selector(key)) && _screen.WaitForTitle(title, ScreenWait);
    }

    private void ClearBlockingDialog()
    {
        _screen.DismissDialog();

        if (_screen.IsVisible(ElementKeys.DIALOG)) ClickByMouse(Selector(ElementKeys.DIALOG_BUTTONS));
    }

    private void WaitForClubResponse(DateTime since)
    {
        var deadline = DateTime.UtcNow + ResponseWait;

        while (Responses(since).Count == 0 && DateTime.UtcNow < deadline) Thread.Sleep(RESPONSE_POLL_MS);
    }

    private IReadOnlyList<ClubResponse> Responses(DateTime since)
    {
        return _network.Since(since, CaptureKind.Club)
            .Select(capture => new ClubResponse(capture.Status, capture.Body)).ToList();
    }

    private bool ClickByMouse(string selector)
    {
        var centre = _driver.ExecuteScript(CentreScript, selector) as string ?? string.Empty;
        var parts = centre.Split(',');
        var clicked = false;

        if (parts.Length == 2 && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            clicked = _mouse.ClickAt(x, y);

        return clicked;
    }

    private static string Selector(ElementKeys key)
    {
        return Elements[key].Item1;
    }

    private static void Persist(ClubReading reading)
    {
        Database.AddClubSnapshot(reading.Players.Count, reading.Outcome.ToString(), reading.Detail);

        if (reading.Outcome == ClubCaptureOutcome.Captured) Database.UpsertClubPlayers(reading.Players);
    }

    private static void Announce(ClubReading reading)
    {
        Console.WriteLine($"Club capture {reading.Outcome}: {reading.Detail}.");
    }
}
