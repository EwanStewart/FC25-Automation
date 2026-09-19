using System.Globalization;
using System.Text.Json;
using Automation.Sbc;
using Automation.Setup;
using Automation.Trading;
using OpenQA.Selenium.Chrome;
using static Automation.Definitions.Fc25Definitions;

namespace Automation;

public sealed class SbcCapture
{
    private const string TileTitlesScript = """
        return JSON.stringify(Array.from(document.querySelectorAll('div.ut-sbc-set-tile-view'))
            .map(tile => ((tile.querySelector('h1.tileTitle') || {}).textContent || '').trim())
            .filter(title => title.length > 0));
        """;

    private const string TileCentreScript = """
        const tile = Array.from(document.querySelectorAll('div.ut-sbc-set-tile-view'))
            .filter(entry => ((entry.querySelector('h1.tileTitle') || {}).textContent || '').trim() === arguments[0])[0];
        if (!tile) return '';
        tile.scrollIntoView({block: 'center'});
        const rect = tile.getBoundingClientRect();
        return (rect.left + rect.width / 2) + ',' + (rect.top + rect.height / 2);
        """;

    private const string CentreScript = """
        const element = document.querySelector(arguments[0]);
        if (!element) return '';
        const rect = element.getBoundingClientRect();
        return (rect.left + rect.width / 2) + ',' + (rect.top + rect.height / 2);
        """;

    private const string SBC_TITLE = "SBC";
    private const string BACK_SELECTOR = "button.ut-navigation-button-control";
    private const int RESPONSE_POLL_MS = 250;
    private const int SCREEN_SETTLE_MS = 1500;
    private const int SCROLL_SETTLE_MS = 800;
    private const int TILE_RETRIES = 4;

    private static readonly TimeSpan ScreenWait = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan SetsWait = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ChallengesWait = TimeSpan.FromSeconds(8);

    private readonly ChromeDriver _driver;
    private readonly Screen _screen;
    private readonly NetworkObserver _network;
    private readonly MouseInput _mouse;
    private readonly Action _reload;

    public SbcCapture(ChromeDriver driver, Screen screen, NetworkObserver network, MouseInput mouse, Action reload)
    {
        _driver = driver;
        _screen = screen;
        _network = network;
        _mouse = mouse;
        _reload = reload;
    }

    public SbcCatalogueReading Capture()
    {
        var fault = ReadinessFault();
        var result = fault.Length > 0
            ? new SbcCatalogueReading(SbcCaptureOutcome.NotObserved, [], [], [], fault)
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

    private SbcCatalogueReading CaptureThroughTheApp()
    {
        var result = Attempt();

        if (NeedsFreshRead(result))
        {
            Console.WriteLine("The SBC screens answered from their cache, reloading the web app for a fresh read.");
            _reload();
            result = Attempt();
        }

        return result;
    }

    private static bool NeedsFreshRead(SbcCatalogueReading reading)
    {
        return reading.Outcome == SbcCaptureOutcome.NotObserved || reading.MissingSets.Count > 0;
    }

    private SbcCatalogueReading Attempt()
    {
        var started = DateTime.UtcNow;
        var titles = EnterSbcHub();

        WaitFor(started, CaptureKind.SbcSets, SetsWait);

        foreach (var title in titles) VisitSet(title);

        return SbcCatalogue.Read(Responses(started, CaptureKind.SbcSets),
            Responses(started, CaptureKind.SbcChallenges));
    }

    private IReadOnlyList<string> EnterSbcHub()
    {
        ClearBlockingDialog();

        var reached = OpenScreen(ElementKeys.SBC_TAB, SBC_TITLE);
        var titles = reached ? SettledTileTitles() : [];

        Console.WriteLine($"SBC hub reached {reached}, {titles.Count} set tiles on screen.");

        return titles;
    }

    private IReadOnlyList<string> SettledTileTitles()
    {
        IReadOnlyList<string> result = [];
        var attempt = 0;

        while (attempt < TILE_RETRIES && result.Count == 0)
        {
            Thread.Sleep(SCREEN_SETTLE_MS);
            result = TileTitles();
            attempt++;
        }

        return result;
    }

    private IReadOnlyList<string> TileTitles()
    {
        var json = _driver.ExecuteScript(TileTitlesScript) as string ?? "[]";
        IReadOnlyList<string> result = [];

        try
        {
            result = JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
        }

        return result;
    }

    private void VisitSet(string title)
    {
        var started = DateTime.UtcNow;
        var opened = OpenSet(title);

        if (opened) WaitFor(started, CaptureKind.SbcChallenges, ChallengesWait);

        Console.WriteLine($"  set '{title}' opened {opened}, screen '{_screen.ReadTitle()}'.");
        LeaveSet();
    }

    private bool OpenSet(string title)
    {
        var clicked = ClickTile(title);

        return clicked && !_screen.WaitForTitle(SBC_TITLE, TimeSpan.FromSeconds(3));
    }

    private void LeaveSet()
    {
        if (_screen.ReadTitle() != SBC_TITLE) ClickByMouse(BACK_SELECTOR);

        _screen.WaitForTitle(SBC_TITLE, ScreenWait);
        Thread.Sleep(SCREEN_SETTLE_MS);
    }

    private bool ClickTile(string title)
    {
        var centre = _driver.ExecuteScript(TileCentreScript, title) as string ?? string.Empty;

        Thread.Sleep(SCROLL_SETTLE_MS);

        var settled = _driver.ExecuteScript(TileCentreScript, title) as string ?? centre;

        return ClickAtCentre(settled);
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

    private void WaitFor(DateTime since, CaptureKind kind, TimeSpan limit)
    {
        var deadline = DateTime.UtcNow + limit;

        while (Responses(since, kind).Count(response => response.Body.Length > 0) == 0 &&
               DateTime.UtcNow < deadline) Thread.Sleep(RESPONSE_POLL_MS);
    }

    private IReadOnlyList<SbcResponse> Responses(DateTime since, CaptureKind kind)
    {
        return _network.Since(since, kind)
            .Select(capture => new SbcResponse(capture.Status, capture.Url, capture.Body)).ToList();
    }

    private bool ClickByMouse(string selector)
    {
        return ClickAtCentre(_driver.ExecuteScript(CentreScript, selector) as string ?? string.Empty);
    }

    private bool ClickAtCentre(string centre)
    {
        var parts = centre.Split(',');
        var clicked = false;

        if (parts.Length == 2 &&
            double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            clicked = _mouse.ClickAt(x, y);

        return clicked;
    }

    private static string Selector(ElementKeys key)
    {
        return Elements[key].Item1;
    }

    private static void Persist(SbcCatalogueReading reading)
    {
        if (reading.Outcome == SbcCaptureOutcome.Captured) new SbcStore().SaveCatalogue(reading);
    }

    private static void Announce(SbcCatalogueReading reading)
    {
        Console.WriteLine($"SBC capture {reading.Outcome}: {reading.Detail}.");
    }
}
