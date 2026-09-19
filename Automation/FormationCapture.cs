using System.Globalization;
using System.Text.Json;
using Automation.Sbc;
using Automation.Setup;
using OpenQA.Selenium.Chrome;
using static Automation.Definitions.Fc25Definitions;

namespace Automation;

public sealed class FormationCapture
{
    private const string CentreScript = """
        const element = document.querySelector(arguments[0]);
        if (!element) return '';
        element.scrollIntoView({block: 'center'});
        const rect = element.getBoundingClientRect();
        return (rect.left + rect.width / 2) + ',' + (rect.top + rect.height / 2);
        """;

    private const string TileCentreScript = """
        const tile = Array.from(document.querySelectorAll('div.tile'))
            .filter(entry => (entry.innerText || '').trim().startsWith(arguments[0]))[0];
        if (!tile) return '';
        tile.scrollIntoView({block: 'center'});
        const rect = tile.getBoundingClientRect();
        return (rect.left + rect.width / 2) + ',' + (rect.top + rect.height / 2);
        """;

    private const string ScreenNameScript = """
        const heading = document.querySelector('.ut-navigation-bar-view h1');
        return heading ? heading.textContent.trim() : '';
        """;

    private const string CarouselReadyScript = """
        return document.querySelector('section.formation-carousel') ? 'ready' : '';
        """;

    private const string CurrentSlideScript = """
        const marker = document.querySelector('section.formation-carousel .tns-liveregion .current');
        return marker ? marker.textContent.trim() : '';
        """;

    private const string SlideIdsScript = """
        const ids = [];
        document.querySelectorAll('section.formation-carousel .tns-item img').forEach(image => {
            const id = (image.getAttribute('src') || '').split('formation').pop().split('.png')[0];
            if (id.length > 0 && ids.indexOf(id) < 0) ids.push(id);
        });
        return JSON.stringify(ids);
        """;

    private const string SlideScript = """
        const slots = Array.from(document.querySelectorAll('div.ut-squad-slot-view'))
            .map(slot => ({
                index: Number(slot.getAttribute('index')),
                label: ((slot.querySelector('div.ut-squad-slot-pedestal-view span.label') || {}).textContent || '').trim()
            }))
            .filter(slot => slot.index < 11)
            .sort((first, second) => first.index - second.index);
        return JSON.stringify({imageId: arguments[0], slots: slots});
        """;

    private const string DirectoryScript = """
        const formations = repositories.Squad.getFormations();
        return JSON.stringify(formations.map(formation => ({
            id: formation.getId(),
            code: formation.getName(),
            display: formation.getDisplayName()
        })));
        """;

    private const string SQUADS_TITLE = "Squads";
    private const string ACTIVE_SQUAD_TILE = "Active Squad";
    private const string SOURCE = "EA FC 27 web app, active squad formation carousel";
    private const string SQUAD_TAB_SELECTOR = "nav.ut-tab-bar button.icon-squad";
    private const string NEXT_SELECTOR = "section.formation-carousel a.tapRight";
    private const string PREVIOUS_SELECTOR = "section.formation-carousel a.tapLeft";
    private const int SCREEN_SETTLE_MS = 1500;
    private const int SLIDE_SETTLE_MS = 850;
    private const int SCREEN_POLL_MS = 250;
    private const int STEP_BUDGET = 60;
    private const int FIRST_SLIDE = 1;

    private static readonly TimeSpan ScreenWait = TimeSpan.FromSeconds(15);

    private readonly ChromeDriver _driver;
    private readonly Screen _screen;
    private readonly MouseInput _mouse;
    private readonly string _outputPath;

    public FormationCapture(ChromeDriver driver, Screen screen, MouseInput mouse, string outputPath)
    {
        _driver = driver;
        _screen = screen;
        _mouse = mouse;
        _outputPath = outputPath;
    }

    public FormationCaptureReading Capture()
    {
        var fault = ReadinessFault();
        var result = fault.Length > 0
            ? new FormationCaptureReading(FormationCaptureOutcome.NotObserved, [], [], fault)
            : CaptureThroughTheApp();

        Persist(result);
        Announce(result);

        return result;
    }

    private string ReadinessFault()
    {
        var result = string.Empty;

        if (!_mouse.Enabled) result = "mouse input is not running";

        return result;
    }

    private FormationCaptureReading CaptureThroughTheApp()
    {
        var reached = EnterActiveSquad();
        var result = reached
            ? WalkTheCarousel()
            : new FormationCaptureReading(FormationCaptureOutcome.NotObserved, [], [],
                "the active squad screen was not reached");

        return result;
    }

    private bool EnterActiveSquad()
    {
        ClearBlockingDialog();

        var onHub = OpenSquadsHub();
        var onSquad = onHub && OpenActiveSquad();

        Console.WriteLine($"Squads hub reached {onHub}, active squad reached {onSquad}.");

        return onSquad;
    }

    private bool OpenSquadsHub()
    {
        _screen.WaitVisible(ElementKeys.NAVIGATION_BAR, ScreenWait);
        Thread.Sleep(SCREEN_SETTLE_MS);

        return ClickByMouse(SQUAD_TAB_SELECTOR) && WaitForScreen(SQUADS_TITLE);
    }

    private bool OpenActiveSquad()
    {
        var centre = _driver.ExecuteScript(TileCentreScript, ACTIVE_SQUAD_TILE) as string ?? string.Empty;

        Thread.Sleep(SCREEN_SETTLE_MS);

        var settled = _driver.ExecuteScript(TileCentreScript, ACTIVE_SQUAD_TILE) as string ?? centre;

        return ClickAtCentre(settled) && WaitForCarousel();
    }

    private FormationCaptureReading WalkTheCarousel()
    {
        var startingSlide = CurrentSlide();
        var slideIds = SlideIds();
        List<string> slides = [];

        try
        {
            StepTo(FIRST_SLIDE);
            Record(slideIds, slides);
        }
        finally
        {
            StepTo(startingSlide);
            Console.WriteLine($"Formation carousel returned to slide {CurrentSlide()} of {slideIds.Count}.");
        }

        return FormationReader.Read(Directory(), slides);
    }

    private void Record(IReadOnlyList<string> slideIds, List<string> slides)
    {
        var slide = FIRST_SLIDE;

        while (slide <= slideIds.Count)
        {
            if (slide > FIRST_SLIDE) Step(1);
            slides.Add(Slide(slideIds, CurrentSlide()));
            slide++;
        }
    }

    private string Slide(IReadOnlyList<string> slideIds, int slide)
    {
        var imageId = slide >= FIRST_SLIDE && slide <= slideIds.Count ? slideIds[slide - 1] : string.Empty;

        return _driver.ExecuteScript(SlideScript, imageId) as string ?? string.Empty;
    }

    private void StepTo(int slide)
    {
        var steps = 0;

        while (CurrentSlide() != slide && steps < STEP_BUDGET)
        {
            Step(CurrentSlide() < slide ? 1 : -1);
            steps++;
        }
    }

    private void Step(int direction)
    {
        ClickByMouse(direction > 0 ? NEXT_SELECTOR : PREVIOUS_SELECTOR);
        Thread.Sleep(SLIDE_SETTLE_MS);
    }

    private int CurrentSlide()
    {
        var text = _driver.ExecuteScript(CurrentSlideScript) as string ?? string.Empty;

        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var slide) ? slide : 0;
    }

    private IReadOnlyList<string> SlideIds()
    {
        var json = _driver.ExecuteScript(SlideIdsScript) as string ?? "[]";
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

    private string Directory()
    {
        return _driver.ExecuteScript(DirectoryScript) as string ?? string.Empty;
    }

    private bool WaitForCarousel()
    {
        var deadline = DateTime.UtcNow + ScreenWait;
        var ready = CarouselReady();

        while (!ready && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(SCREEN_POLL_MS);
            ready = CarouselReady();
        }

        return ready;
    }

    private bool CarouselReady()
    {
        return (_driver.ExecuteScript(CarouselReadyScript) as string ?? string.Empty).Length > 0;
    }

    private bool WaitForScreen(string name)
    {
        var deadline = DateTime.UtcNow + ScreenWait;
        var matched = ScreenName() == name;

        while (!matched && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(SCREEN_POLL_MS);
            matched = ScreenName() == name;
        }

        return matched;
    }

    private string ScreenName()
    {
        return _driver.ExecuteScript(ScreenNameScript) as string ?? string.Empty;
    }

    private void ClearBlockingDialog()
    {
        _screen.DismissDialog();

        if (_screen.IsVisible(ElementKeys.DIALOG)) ClickByMouse(Selector(ElementKeys.DIALOG_BUTTONS));
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

    private void Persist(FormationCaptureReading reading)
    {
        if (reading.Outcome == FormationCaptureOutcome.Captured)
            File.WriteAllText(_outputPath,
                FormationLayouts.ToJson(reading.Layouts, DateTime.UtcNow.ToString("yyyy-MM-dd"), SOURCE));
    }

    private static void Announce(FormationCaptureReading reading)
    {
        Console.WriteLine($"Formation capture {reading.Outcome}: {reading.Detail}.");
    }
}
