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
            .filter(entry => !entry.disabled && (entry.textContent || '').trim() === arguments[0] &&
                entry.getBoundingClientRect().width > 0)[0];
        if (!button) return '';
        button.scrollIntoView({block: 'center'});
        const rect = button.getBoundingClientRect();
        if (rect.width === 0 || rect.height === 0) return '';
        return (rect.left + rect.width / 2) + ',' + (rect.top + rect.height / 2);
        """;

    private const string LiveSquadScript = """
        function holder(node, depth) {
            if (!node || depth > 8) return null;
            if (node._squad && node._challengeId) return node;
            const kids = node.childViewControllers || [];
            for (let i = 0; i < kids.length; i++) {
                const found = holder(kids[i], depth + 1);
                if (found) return found;
            }
            return null;
        }
        const flows = (window.getAppMain && getAppMain().getRootViewController().gameflowControllers) || [];
        let owner = null;
        for (let i = 0; i < flows.length && !owner; i++) owner = holder(flows[i], 0);
        if (!owner) return '';
        const slots = owner._squad.getSlots() || [];
        const players = slots.map(slot => ({
            index: slot.index,
            itemData: {
                id: slot.item ? slot.item.id : 0,
                assetId: slot.item ? (slot.item.definitionId || 0) : 0,
                rating: slot.item ? slot.item.rating : 0,
                preferredPosition: (slot.position || {}).typeName || '',
                itemState: slot.item && slot.item.id ? 'free' : 'invalid'
            }
        }));
        return JSON.stringify({
            challengeId: owner._challengeId,
            squad: {
                formation: owner._squad._formation ? owner._squad._formation.name : '',
                players: players
            }
        });
        """;

    private const string ClubSearchCentreScript = """
        const slotWanted = Number(arguments[0]);
        const label = arguments[1];
        function bound(node, depth) {
            if (!node || depth > 8) return -1;
            if (node.slot && typeof node.slot.index === 'number') return node.slot.index;
            const kids = node.childViewControllers || [];
            for (let i = 0; i < kids.length; i++) {
                const found = bound(kids[i], depth + 1);
                if (found >= 0) return found;
            }
            return -1;
        }
        function panel(node, depth) {
            if (!node || depth > 8) return null;
            if (node.pinnedItemVC !== undefined && node.clubSearchType !== undefined) return node;
            const kids = node.childViewControllers || [];
            for (let i = 0; i < kids.length; i++) {
                const found = panel(kids[i], depth + 1);
                if (found) return found;
            }
            return null;
        }
        const flows = (window.getAppMain && getAppMain().getRootViewController().gameflowControllers) || [];
        let owner = null;
        for (let i = 0; i < flows.length && !owner; i++) owner = panel(flows[i], 0);
        if (!owner || bound(owner, 0) !== slotWanted) return '';
        const button = Array.from(document.querySelectorAll('div.ut-club-search-filters-view button'))
            .filter(entry => !entry.disabled && (entry.textContent || '').trim() === label &&
                entry.getBoundingClientRect().width > 0)[0];
        if (!button) return '';
        button.scrollIntoView({block: 'center'});
        const rect = button.getBoundingClientRect();
        if (rect.width === 0 || rect.height === 0) return '';
        return (rect.left + rect.width / 2) + ',' + (rect.top + rect.height / 2);
        """;

    private const string PickerCentreScript = """
        const slotWanted = Number(arguments[0]);
        const itemWanted = String(arguments[1]);
        function picker(node, depth) {
            if (!node || depth > 8) return null;
            if (node.clubViewModel && node.clubViewModel._collection) return node;
            const kids = node.childViewControllers || [];
            for (let i = 0; i < kids.length; i++) {
                const found = picker(kids[i], depth + 1);
                if (found) return found;
            }
            return null;
        }
        const flows = (window.getAppMain && getAppMain().getRootViewController().gameflowControllers) || [];
        let owner = null;
        for (let i = 0; i < flows.length && !owner; i++) owner = picker(flows[i], 0);
        if (!owner) return '';
        if (typeof owner.slotIndex === 'number' && owner.slotIndex !== slotWanted) return '';
        const collection = owner.clubViewModel._collection || [];
        const rows = document.querySelectorAll('li.listFUTItem.has-action');
        if (rows.length !== collection.length) return '';
        let wanted = -1;
        for (let i = 0; i < collection.length; i++) if (String(collection[i].id) === itemWanted) wanted = i;
        if (wanted < 0) return '';
        const row = rows[wanted];
        if (!row) return '';
        row.scrollIntoView({block: 'center'});
        const action = row.querySelector('button.btnAction') || row;
        const rect = action.getBoundingClientRect();
        if (rect.width === 0 || rect.height === 0) return '';
        return (rect.left + rect.width / 2) + ',' + (rect.top + rect.height / 2);
        """;

    private const string FilterControlsScript = """
        return JSON.stringify(Array.from(document.querySelectorAll(
            'div.ut-club-search-filters-view div.ut-search-filter-control')).map(function (control, index) {
            const image = control.querySelector('img.ut-search-filter-control--row-image');
            const label = control.querySelector('span.label');
            const button = control.querySelector('button.ut-search-filter-control--row-button');
            const box = button ? button.getBoundingClientRect() : {width: 0, height: 0};
            return {
                Index: index,
                Label: label ? (label.textContent || '').trim() : '',
                Image: image ? (image.getAttribute('src') || '') : '',
                Clearable: button !== null && !button.disabled && box.width > 0 && box.height > 0
            };
        }));
        """;

    private const string FilterClearCentreScript = """
        const control = document.querySelectorAll(
            'div.ut-club-search-filters-view div.ut-search-filter-control')[Number(arguments[0])];
        if (!control) return '';
        const button = control.querySelector('button.ut-search-filter-control--row-button');
        if (!button || button.disabled) return '';
        button.scrollIntoView({block: 'center'});
        const rect = button.getBoundingClientRect();
        if (rect.width === 0 || rect.height === 0) return '';
        return (rect.left + rect.width / 2) + ',' + (rect.top + rect.height / 2);
        """;

    private const string PickerItemsScript = """
        function picker(node, depth) {
            if (!node || depth > 8) return null;
            if (node.clubViewModel && node.clubViewModel._collection) return node;
            const kids = node.childViewControllers || [];
            for (let i = 0; i < kids.length; i++) {
                const found = picker(kids[i], depth + 1);
                if (found) return found;
            }
            return null;
        }
        const flows = (window.getAppMain && getAppMain().getRootViewController().gameflowControllers) || [];
        let owner = null;
        for (let i = 0; i < flows.length && !owner; i++) owner = picker(flows[i], 0);
        if (!owner) return 'no club picker is open';
        const collection = owner.clubViewModel._collection || [];
        const rows = document.querySelectorAll('li.listFUTItem.has-action').length;
        return rows + ' row(s) for ' + collection.length + ' card(s): ' + collection
            .map(item => (item._staticData ? item._staticData.name : '?') + ' ' + item.rating + ' ' + item.id)
            .join(', ');
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
    private const string CLUB_SEARCH = "Search";
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
    private static readonly TimeSpan ControlWait = TimeSpan.FromSeconds(25);
    private static readonly TimeSpan PanelWait = TimeSpan.FromSeconds(6);

    private readonly ChromeDriver driver_;
    private readonly Screen screen_;
    private readonly NetworkObserver network_;
    private readonly MouseInput mouse_;
    private readonly ChallengeRoute route_;

    private SquadView? seen_;

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

        var answered = Remember(Fresh(challengeId) ?? Settled(since, challengeId));
        var view = answered ?? Remembered(challengeId);

        if (view is null)
            throw new InvalidOperationException(
                $"The squad for challenge {challengeId} ('{route_.ChallengeName}') never came back from the app.");

        Announce(view, answered is null);

        return view;
    }

    private void Announce(SquadView view, bool cached)
    {
        var source = cached ? " served from the client cache, so it is the squad this run last read" : string.Empty;

        Console.WriteLine($"  squad for challenge {view.ChallengeId} ('{route_.ChallengeName}'): " +
                          $"formation {view.Formation}, {view.Slots.Count(slot => slot.Filled)} of " +
                          $"{view.Slots.Count} slots filled{source}.");
    }

    private SquadView? Remember(SquadView? view)
    {
        if (view is not null) seen_ = view;

        return view;
    }

    private SquadView? Remembered(int challengeId)
    {
        return seen_ is not null && seen_.ChallengeId == challengeId ? seen_ : null;
    }

    public void Place(int slotIndex, SquadTarget target)
    {
        ForbiddenControls.Require(ADD_PLAYER);
        ForbiddenControls.Require(CLUB_SEARCH);

        var slot = slotIndex.ToString(CultureInfo.InvariantCulture);
        var asked = Asked(slot);
        var searched = asked && Pressed(ControlWait, ClubSearchCentreScript, slot, CLUB_SEARCH);

        Walked(slotIndex, target, asked, searched);

        if (searched) Choose(slotIndex, slot, target);

        Thread.Sleep(SCREEN_SETTLE_MS);
    }

    public SquadView Read(int challengeId)
    {
        return Remember(Fresh(challengeId)) ?? new SquadView(challengeId, string.Empty, []);
    }

    private bool Asked(string slot)
    {
        var attempt = 0;
        var asked = false;

        while (!asked && attempt < OPEN_ATTEMPTS)
        {
            Pressed(ControlWait, SlotCentreScript, slot);
            asked = Pressed(PanelWait, PanelButtonCentreScript, ADD_PLAYER);
            attempt++;
        }

        return asked;
    }

    private void Walked(int slotIndex, SquadTarget target, bool asked, bool searched)
    {
        if (!searched)
            Console.WriteLine($"  slot {slotIndex} ({target.Name}): '{ADD_PLAYER}' pressed {asked}, " +
                              $"club search started {searched}.");
    }

    private void Choose(int slotIndex, string slot, SquadTarget target)
    {
        var item = target.ItemId.ToString(CultureInfo.InvariantCulture);

        if (!Pressed(ControlWait, PickerCentreScript, slot, item)) OutOfPosition(slotIndex, slot, target, item);
    }

    private void OutOfPosition(int slotIndex, string slot, SquadTarget target, string item)
    {
        var widened = Widen(slotIndex) && Pressed(ControlWait, ClubSearchCentreScript, slot, CLUB_SEARCH);

        if (widened && Pressed(ControlWait, PickerCentreScript, slot, item)) Reached(slotIndex, target);
        else Missed(slotIndex, target);
    }

    private bool Widen(int slotIndex)
    {
        var cleared = Cleared();

        Widened(slotIndex, cleared);

        return cleared;
    }

    private bool Cleared()
    {
        var control = ClubSearchFilters.PositionControl(SettledFilterControls());

        return control != ClubSearchFilters.NOTHING &&
               Pressed(PanelWait, FilterClearCentreScript, control.ToString(CultureInfo.InvariantCulture));
    }

    private void Widened(int slotIndex, bool cleared)
    {
        Console.WriteLine(cleared
            ? $"  slot {slotIndex}: cleared the position filter so the club picker offers every owned card."
            : $"  slot {slotIndex}: the club picker showed no position filter that could be cleared.");
    }

    private void Reached(int slotIndex, SquadTarget target)
    {
        Console.WriteLine($"  slot {slotIndex}: {target.Name} ({target.ItemId}) was chosen out of position.");
    }

    private IReadOnlyList<FilterControl> SettledFilterControls()
    {
        var controls = FilterControls();
        var attempt = 0;

        while (ClubSearchFilters.PositionControl(controls) == ClubSearchFilters.NOTHING && attempt < OPEN_ATTEMPTS)
        {
            Thread.Sleep(SCREEN_SETTLE_MS);
            controls = FilterControls();
            attempt++;
        }

        return controls;
    }

    private IReadOnlyList<FilterControl> FilterControls()
    {
        var json = driver_.ExecuteScript(FilterControlsScript) as string ?? "[]";
        IReadOnlyList<FilterControl> result = [];

        try
        {
            result = JsonSerializer.Deserialize<List<FilterControl>>(json) ?? [];
        }
        catch (JsonException)
        {
        }

        return result;
    }

    private void Missed(int slotIndex, SquadTarget target)
    {
        var listed = driver_.ExecuteScript(PickerItemsScript) as string ?? "nothing";

        Console.WriteLine($"  slot {slotIndex}: the club picker never offered {target.Name} " +
                          $"({target.ItemId}); it held {listed}.");
    }

    private bool Pressed(TimeSpan wait, string script, params object[] arguments)
    {
        var centre = Waited(wait, script, arguments);

        Thread.Sleep(SCROLL_SETTLE_MS);

        var confirmed = centre.Length > 0 ? Centre(script, arguments) : string.Empty;

        return ClickAtCentre(confirmed.Length > 0 ? confirmed : centre);
    }

    private string Waited(TimeSpan wait, string script, object[] arguments)
    {
        var deadline = DateTime.UtcNow + wait;
        var centre = Centre(script, arguments);

        while (centre.Length == 0 && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(RESPONSE_POLL_MS);
            centre = Centre(script, arguments);
        }

        return centre;
    }

    private string Centre(string script, object[] arguments)
    {
        return driver_.ExecuteScript(script, arguments) as string ?? string.Empty;
    }

    private SquadView? Fresh(int challengeId)
    {
        var deadline = DateTime.UtcNow + SquadWait;
        var view = LiveSquad(challengeId);

        while (view is null && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(RESPONSE_POLL_MS);
            view = LiveSquad(challengeId);
        }

        return view;
    }

    private SquadView? LiveSquad(int challengeId)
    {
        var view = SquadReader.Read(driver_.ExecuteScript(LiveSquadScript) as string ?? string.Empty);

        return view is not null && view.ChallengeId == challengeId ? view : null;
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
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) &&
            x > 0 && y > 0)
            clicked = mouse_.ClickAt(x, y);

        return clicked;
    }
}
