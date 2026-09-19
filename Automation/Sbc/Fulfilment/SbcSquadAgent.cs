using System.Globalization;
using System.Text.Json;
using Automation.Sbc;
using Automation.Sbc.Fulfilment;
using Automation.Setup;
using Automation.Trading;
using OpenQA.Selenium.Chrome;
using static Automation.Definitions.Fc25Definitions;

namespace Automation;

public sealed record ChallengeRoute(int ChallengeId, string SetName, string ChallengeName);

public sealed class SbcSquadAgent : ISquadAgent
{
    private const string ScreenNameScript = """
        const heading = document.querySelector('.ut-navigation-bar-view h1');
        return heading ? heading.textContent.trim() : '';
        """;

    private const string TileCentreScript = """
        const tile = Array.from(document.querySelectorAll(arguments[0]))
            .filter(entry => ((entry.querySelector('h1.tileTitle') || entry.querySelector('.tileTitle') || {}).textContent || '').trim() === arguments[1])[0];
        if (!tile) return '';
        tile.scrollIntoView({block: 'center'});
        const rect = tile.getBoundingClientRect();
        return (rect.left + rect.width / 2) + ',' + (rect.top + rect.height / 2);
        """;

    private const string TitledCentreScript = """
        const wanted = arguments[0];
        const leaves = Array.from(document.querySelectorAll('h1, h2, h3, span, div, li'))
            .filter(node => node.children.length === 0 && (node.textContent || '').trim() === wanted);
        if (leaves.length === 0) return '';
        let node = leaves[leaves.length - 1];
        for (let depth = 0; depth < 6 && node.parentElement; depth++) {
            const box = node.getBoundingClientRect();
            if (box.width > 120 && box.height > 40) break;
            node = node.parentElement;
        }
        node.scrollIntoView({block: 'center'});
        const rect = node.getBoundingClientRect();
        return (rect.left + rect.width / 2) + ',' + (rect.top + rect.height / 2);
        """;

    private const string ScreenTitlesScript = """
        return JSON.stringify(Array.from(document.querySelectorAll('h1, h2, h3, span.label, div.tileTitle'))
            .map(node => (node.textContent || '').trim())
            .filter(text => text.length > 0 && text.length < 60)
            .slice(0, 40));
        """;

    private const string SlotCentreScript = """
        const slot = document.querySelector('div.ut-squad-pitch-view div.ut-squad-slot-view[index="' + arguments[0] + '"]');
        if (!slot) return '';
        const card = slot.querySelector('.player.item') || slot;
        card.scrollIntoView({block: 'center'});
        const rect = card.getBoundingClientRect();
        return (rect.left + rect.width / 2) + ',' + (rect.top + rect.height / 2);
        """;

    private const string PanelButtonCentreScript = """
        const button = Array.from(document.querySelectorAll('div.DetailPanel button'))
            .filter(entry => (entry.textContent || '').trim() === arguments[0])[0];
        if (!button) return '';
        button.scrollIntoView({block: 'center'});
        const rect = button.getBoundingClientRect();
        return (rect.left + rect.width / 2) + ',' + (rect.top + rect.height / 2);
        """;

    private const string PickerItemCentreScript = """
        const items = document.querySelectorAll('div.DetailPanel li.listFUTItem, section.ui-layout-right li.listFUTItem');
        const item = items[arguments[0]];
        if (!item) return '';
        item.scrollIntoView({block: 'center'});
        const rect = item.getBoundingClientRect();
        return (rect.left + rect.width / 2) + ',' + (rect.top + rect.height / 2);
        """;

    private const string PickerCountScript = """
        return document.querySelectorAll('div.DetailPanel li.listFUTItem, section.ui-layout-right li.listFUTItem').length;
        """;

    private const string EntryLabelsScript = """
        return JSON.stringify(Array.from(document.querySelectorAll(arguments[0]))
            .filter(button => !button.disabled)
            .map(button => (button.textContent || '').trim())
            .filter(text => text.length > 0));
        """;

    private const string EntryCentreScript = """
        const button = Array.from(document.querySelectorAll(arguments[0]))
            .filter(entry => !entry.disabled && (entry.textContent || '').trim() === arguments[1])[0];
        if (!button) return '';
        button.scrollIntoView({block: 'center'});
        const rect = button.getBoundingClientRect();
        return (rect.left + rect.width / 2) + ',' + (rect.top + rect.height / 2);
        """;

    private const string ADD_PLAYER = "Add Player";
    private const string SBC_TITLE = "SBC";

    private const string ENTRY_SELECTOR =
        "div.ut-sbc-requirements-view footer button, div.ut-sbc-requirements-popup footer button";

    private const int RESPONSE_POLL_MS = 250;
    private const int SCREEN_SETTLE_MS = 1500;
    private const int SCROLL_SETTLE_MS = 800;
    private const int OPEN_ATTEMPTS = 3;

    private static readonly string[] SET_TILE_SELECTORS = ["div.ut-sbc-set-tile-view"];

    private static readonly string[] CHALLENGE_TILE_SELECTORS =
    [
        "div.ut-sbc-challenge-tile-view",
        "div.ut-sbc-challenge-table-row-view",
        "div.ut-sbc-set-tile-view"
    ];

    private static readonly TimeSpan ScreenWait = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan SquadWait = TimeSpan.FromSeconds(12);

    private readonly ChromeDriver driver_;
    private readonly Screen screen_;
    private readonly NetworkObserver network_;
    private readonly MouseInput mouse_;
    private readonly ChallengeRoute route_;

    public SbcSquadAgent(ChromeDriver driver, Screen screen, NetworkObserver network, MouseInput mouse,
        ChallengeRoute route)
    {
        driver_ = driver;
        screen_ = screen;
        network_ = network;
        mouse_ = mouse;
        route_ = route;
    }

    public SquadView Open(int challengeId)
    {
        var since = DateTime.UtcNow;

        EnterChallenge();

        var view = Settled(since, challengeId);

        if (view is null)
            throw new InvalidOperationException(
                $"The squad for challenge {challengeId} ('{route_.ChallengeName}') never came back from the app.");

        return view;
    }

    public void Place(int slotIndex, SquadTarget target)
    {
        ForbiddenControls.Require(ADD_PLAYER);

        var opened = ClickAtCentre(driver_.ExecuteScript(SlotCentreScript,
            slotIndex.ToString(CultureInfo.InvariantCulture)) as string ?? string.Empty);

        Thread.Sleep(SCREEN_SETTLE_MS);

        var since = DateTime.UtcNow;
        var asked = opened && ClickPanelButton(ADD_PLAYER);

        Thread.Sleep(SCREEN_SETTLE_MS);

        if (asked) Choose(target, since);
    }

    public SquadView Read(int challengeId)
    {
        var since = DateTime.UtcNow - TimeSpan.FromSeconds(30);

        return Settled(since, challengeId) ??
               new SquadView(challengeId, string.Empty, []);
    }

    private void Choose(SquadTarget target, DateTime since)
    {
        var index = PickerIndex(target, since);

        if (index >= 0)
        {
            ClickAtCentre(driver_.ExecuteScript(PickerItemCentreScript, index) as string ?? string.Empty);
            Thread.Sleep(SCREEN_SETTLE_MS);
        }
        else
        {
            Console.WriteLine($"Slot {target.SlotIndex}: '{target.Name}' is not in the club picker list.");
        }
    }

    private int PickerIndex(SquadTarget target, DateTime since)
    {
        var deadline = DateTime.UtcNow + SquadWait;
        var items = PickerItems(since);

        while (items.Count == 0 && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(RESPONSE_POLL_MS);
            items = PickerItems(since);
        }

        var shown = Convert.ToInt32(driver_.ExecuteScript(PickerCountScript) ?? 0);
        var index = items.ToList().FindIndex(player => player.Id == target.ItemId);

        return index >= 0 && index < shown ? index : -1;
    }

    private IReadOnlyList<ClubPlayer> PickerItems(DateTime since)
    {
        return network_.Since(since, CaptureKind.Club).Where(capture => capture.Body.Length > 0)
            .Select(capture => ClubInventory.Parse(capture.Body)).LastOrDefault(players => players.Count > 0) ?? [];
    }

    private void EnterChallenge()
    {
        RequireMouse();
        OpenHub();
        OpenTile(SET_TILE_SELECTORS, route_.SetName);
        OpenTile(CHALLENGE_TILE_SELECTORS, route_.ChallengeName);
        OpenSquad();
    }

    private void OpenSquad()
    {
        var labels = SettledEntryLabels();
        var wanted = ChallengeEntry.Opening(labels);

        if (wanted.Length == 0)
            throw new InvalidOperationException(
                $"Nothing on screen '{ScreenName()}' opens the squad for '{route_.ChallengeName}'; " +
                $"the challenge pane offers [{string.Join(", ", labels)}].");

        Entered(wanted, Press(wanted));
    }

    private IReadOnlyList<string> SettledEntryLabels()
    {
        var labels = EntryLabels();
        var attempt = 0;

        while (ChallengeEntry.Opening(labels).Length == 0 && attempt < OPEN_ATTEMPTS)
        {
            Thread.Sleep(SCREEN_SETTLE_MS);
            labels = EntryLabels();
            attempt++;
        }

        return labels;
    }

    private IReadOnlyList<string> EntryLabels()
    {
        var json = driver_.ExecuteScript(EntryLabelsScript, ENTRY_SELECTOR) as string ?? "[]";
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

    private bool Press(string label)
    {
        var centre = driver_.ExecuteScript(EntryCentreScript, ENTRY_SELECTOR, label) as string ?? string.Empty;

        Thread.Sleep(SCROLL_SETTLE_MS);

        var settled = driver_.ExecuteScript(EntryCentreScript, ENTRY_SELECTOR, label) as string ?? centre;

        return ClickAtCentre(settled);
    }

    private void Entered(string label, bool pressed)
    {
        Console.WriteLine($"  pressed '{label}' for '{route_.ChallengeName}', now on screen '{ScreenName()}'.");

        if (!pressed)
            throw new InvalidOperationException(
                $"The control '{label}' for '{route_.ChallengeName}' would not take a click, so the squad never opened.");
    }

    private void RequireMouse()
    {
        if (!mouse_.Enabled)
            throw new InvalidOperationException(
                "Real mouse input is not running, so no SBC tile can be clicked and the challenge cannot be opened.");
    }

    private void OpenHub()
    {
        screen_.DismissDialog();
        screen_.WaitVisible(ElementKeys.SBC_TAB, ScreenWait);
        Thread.Sleep(SCREEN_SETTLE_MS);
        ClickByMouse(Elements[ElementKeys.SBC_TAB].Item1);
        WaitForScreen(SBC_TITLE);
    }

    private void OpenTile(IReadOnlyList<string> selectors, string title)
    {
        var clicked = false;
        var attempt = 0;

        while (!clicked && attempt < OPEN_ATTEMPTS)
        {
            clicked = selectors.Any(selector => ClickTile(selector, title)) || ClickTitled(title);
            attempt++;

            if (!clicked) Thread.Sleep(SCREEN_SETTLE_MS);
        }

        Report(title, clicked);
        Thread.Sleep(SCREEN_SETTLE_MS);
    }

    private bool ClickTitled(string title)
    {
        ForbiddenControls.Require(title);

        var centre = driver_.ExecuteScript(TitledCentreScript, title) as string ?? string.Empty;

        Thread.Sleep(SCROLL_SETTLE_MS);

        var settled = driver_.ExecuteScript(TitledCentreScript, title) as string ?? centre;

        return ClickAtCentre(settled);
    }

    private void Report(string title, bool clicked)
    {
        Console.WriteLine(clicked
            ? $"  opened '{title}', now on screen '{ScreenName()}'."
            : $"  nothing titled '{title}' on screen '{ScreenName()}', which shows {Titles()}.");
    }

    private string Titles()
    {
        return driver_.ExecuteScript(ScreenTitlesScript) as string ?? "nothing";
    }

    private bool ClickTile(string selector, string title)
    {
        var centre = driver_.ExecuteScript(TileCentreScript, selector, title) as string ?? string.Empty;

        Thread.Sleep(SCROLL_SETTLE_MS);

        var settled = driver_.ExecuteScript(TileCentreScript, selector, title) as string ?? centre;

        return ClickAtCentre(settled);
    }

    private bool ClickPanelButton(string label)
    {
        ForbiddenControls.Require(label);

        return ClickAtCentre(driver_.ExecuteScript(PanelButtonCentreScript, label) as string ?? string.Empty);
    }

    private SquadView? Settled(DateTime since, int challengeId)
    {
        var deadline = DateTime.UtcNow + SquadWait;
        var view = Latest(since, challengeId);

        while (view is null && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(RESPONSE_POLL_MS);
            view = Latest(since, challengeId);
        }

        return view;
    }

    private SquadView? Latest(DateTime since, int challengeId)
    {
        return network_.Since(since, CaptureKind.SbcSquad).Where(capture => capture.Body.Length > 0)
            .Select(Identified).LastOrDefault(view => view is not null && view.ChallengeId == challengeId);
    }

    private static SquadView? Identified(Capture capture)
    {
        var view = SquadReader.Read(capture.Body);

        return view is null || view.ChallengeId > 0
            ? view
            : view with { ChallengeId = SquadReader.ChallengeIn(capture.Url) };
    }

    private bool WaitForScreen(string name)
    {
        var deadline = DateTime.UtcNow + ScreenWait;
        var matched = ScreenName() == name;

        while (!matched && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(RESPONSE_POLL_MS);
            matched = ScreenName() == name;
        }

        return matched;
    }

    private string ScreenName()
    {
        return driver_.ExecuteScript(ScreenNameScript) as string ?? string.Empty;
    }

    private bool ClickByMouse(string selector)
    {
        return screen_.Click(Screen.Locator(selector), ScreenWait);
    }

    private bool ClickAtCentre(string centre)
    {
        var parts = centre.Split(',');
        var clicked = false;

        if (parts.Length == 2 &&
            double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            clicked = mouse_.ClickAt(x, y);

        return clicked;
    }
}
