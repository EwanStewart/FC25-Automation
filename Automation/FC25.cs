using Automation.Flow;
using Automation.Setup;
using Automation.Trading;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.Text.Json;
using static Automation.Definitions.Fc25Definitions;
using static Automation.Trading.BiddingStrategy;

namespace Automation;

public class Fc25 : IDisposable
{
    private const string FcUrl = @"https://www.ea.com/fifa/ultimate-team/web-app/";
    private const uint MAX_TRANSFER_TARGETS = 49;
    private const int LISTINGS_BEFORE_REFRESH = 10;
    private const int MAX_LISTING_ATTEMPTS = 200;
    private const int MAX_WON_ITEM_ATTEMPTS = 50;
    private const int MAX_CREDENTIAL_ATTEMPTS = 2;

    private static readonly TimeSpan StandardWait = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ShortWait = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan LoginBudget = TimeSpan.FromSeconds(120);

    private readonly ChromeDriver _driver;
    private readonly Screen _screen;
    private readonly NetworkObserver _network = new();
    private readonly string _user;
    private readonly uint _maxBids;
    private readonly string _smokeTarget;
    private readonly Dictionary<string, int> _credentialAttempts = new();
    private readonly HashSet<string> _bidNamesThisRun = new();
    private readonly Random _random = new();
    private PassPacing _pacing = new(PAGE_TURN_GAP_MS, COMPARE_READ_GAP_MS);
    private DateTime _passStarted = DateTime.UtcNow;
    private DateTime _lastRefresh = DateTime.MinValue;
    private Dictionary<string, uint> _snipeEstimates = new();
    private string _segment = string.Empty;
    private double _calibrationRatio = 1.0;
    private uint _segmentBidAllowance = uint.MaxValue;
    private uint _segmentBidsPlaced;
    private int _lastAskCount;
    private int _rowsConsidered;
    private int _compareReads;
    private uint _total;
    private uint _listed;
    private uint _bidsPlaced;
    private uint _coinBalance;
    private uint _coinsCommitted;

    #region Constructor

    public Fc25(string configuration, bool smokeTest, string smokeTarget = "all")
    {
        _user = configuration;
        _maxBids = smokeTest ? 1u : uint.MaxValue;
        _smokeTarget = smokeTarget;
        Browser browser = new(configuration);
        _driver = browser.Chrome;
        _screen = new Screen(_driver);

        if (NETWORK_OBSERVER) _network.Start(Browser.DebuggerHttp);

        try
        {
            EnsureLoggedIn();
            GetCoinTotal();

            if (smokeTest)
                SmokeTestRoutine();
            else
                ListAndBidRoutine();
        }
        catch (RunStoppedException exception)
        {
            Console.WriteLine($"Run stopped: {exception.Message}");
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
        }
    }

    #endregion

    public void Dispose()
    {
        Console.WriteLine(_network.Summary());

        foreach (var failure in _network.Failures())
            Database.AddSnipeEvent(failure.Kind.ToString(), "http", (uint)failure.Status, null, null, null,
                failure.Url.Length > 200 ? failure.Url[..200] : failure.Url);

        _network.Dispose();

        try
        {
            _driver.Quit();
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Driver shutdown failed: {exception.Message}");
        }
    }

    #region Login

    private void EnsureLoggedIn()
    {
        var deadline = DateTime.UtcNow + LoginBudget;
        var step = LoginFlow.NextStep(ReadLoginScreen());

        if (step != LoginStep.Done && !_driver.Url.Contains("ea.com")) _driver.Navigate().GoToUrl(FcUrl);

        while (step != LoginStep.Done && DateTime.UtcNow < deadline)
        {
            PerformLoginStep(step);
            step = LoginFlow.NextStep(ReadLoginScreen());
        }

        if (step != LoginStep.Done) throw new TimeoutException($"Login did not complete. Last step: {step}.");
    }

    private LoginScreen ReadLoginScreen()
    {
        return new LoginScreen(
            _screen.IsVisible(ElementKeys.NAVIGATION_BAR),
            _screen.IsVisible(ElementKeys.CONTINUE),
            _screen.IsVisible(ElementKeys.PASSWORD_INPUT),
            _screen.IsVisible(ElementKeys.EMAIL_INPUT),
            _screen.IsVisible(ElementKeys.INITIAL_LOGIN),
            _screen.IsVisible(ElementKeys.UNSUPPORTED_BROWSER),
            _screen.IsShieldShowing());
    }

    private void PerformLoginStep(LoginStep step)
    {
        switch (step)
        {
            case LoginStep.ClickLogin:
                _screen.TryClick(ElementKeys.INITIAL_LOGIN, ShortWait);
                break;
            case LoginStep.EnterEmail:
                SubmitCredential(ElementKeys.EMAIL_INPUT, "FC_EMAIL");
                break;
            case LoginStep.EnterPassword:
                SubmitCredential(ElementKeys.PASSWORD_INPUT, "FC_PASSWORD");
                break;
            case LoginStep.Continue:
                _screen.TryClick(ElementKeys.CONTINUE, ShortWait);
                break;
            case LoginStep.Unsupported:
                throw new InvalidOperationException("The web app reports an unsupported browser.");
        }

        Thread.Sleep(1500);
    }

    private void SubmitCredential(ElementKeys inputKey, string secretName)
    {
        var secret = Utility.Utility.GetSecret(secretName);
        var attempts = _credentialAttempts.GetValueOrDefault(secretName) + 1;
        _credentialAttempts[secretName] = attempts;

        if (secret.Length == 0) throw new InvalidOperationException($"{secretName} is not set in .env.");
        if (attempts > MAX_CREDENTIAL_ATTEMPTS)
            throw new InvalidOperationException($"{secretName} was rejected {MAX_CREDENTIAL_ATTEMPTS} times.");

        var input = _screen.WaitVisible(inputKey, ShortWait);

        if (input != null)
        {
            input.Clear();
            input.SendKeys(secret);
            _screen.Click(ElementKeys.SECOND_LOGIN, StandardWait);
        }
    }

    #endregion

    #region Master Routines

    private void SmokeTestRoutine()
    {
        var clubItems = _smokeTarget is "all" or "club";
        var players = _smokeTarget is "all" or "players";

        MaintainTransfers();

        if (clubItems) RunClubItemBidPass(true);
        if (clubItems && _bidsPlaced == 0) RunClubItemBidPass(false);
        if (players && _bidsPlaced == 0) RunSnipePass();
    }

    public void RunCycleSafely()
    {
        try
        {
            _bidsPlaced = 0;
            _bidNamesThisRun.Clear();
            GetCoinTotal();
            RunCycle();
        }
        catch (RunStoppedException exception)
        {
            Console.WriteLine($"Run stopped: {exception.Message}");
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
        }
    }

    public void RunCycle()
    {
        MaintainTransfers();
        RunClubItemBidPass(true);

        if (HasBidCapacity()) RunClubItemBidPass(false);
        if (HasBidCapacity()) RunSnipePass();
    }

    private void MaintainTransfers()
    {
        var started = DateTime.UtcNow;

        Utility.Utility.RetryAction(ClearItemsFromTransferTargets);
        Utility.Utility.RetryAction(ClearSoldItemsFromTransferList);
        Utility.Utility.RetryAction(ListItemsFromTransferList);

        Console.WriteLine($"Maintenance took {(DateTime.UtcNow - started).TotalSeconds:F0} s.");
    }

    private void ListAndBidRoutine()
    {
        RunCycle();
    }

    private void RunClubItemBidPass(bool badges)
    {
        RunTimedPass(badges ? Segments.BADGES : Segments.KITS, Segments.ROLE_CLUB, () => BidOnSilverClubItems(badges));
    }

    private void RunPlayerPasses()
    {
        var records = Database.GetSegmentRecords(SEGMENT_WINDOW_DAYS);
        var cooling = CoolingNations(records);
        var last = Database.GetLastExploratoryNation();
        var exploratory = NationRotation.NextExploratory(NationRotation.NATIONS, last, cooling);
        var best = NationRotation.BestPerformer(records.Values, cooling);

        Console.WriteLine(
            $"Nation rotation: best {best ?? "none"}, exploratory {exploratory ?? "none"}, last exploratory {last ?? "none"}, cooling [{string.Join(", ", cooling)}].");

        if (best != null && best != exploratory) RunPlayerPass(best, Segments.ROLE_BEST);
        if (exploratory != null && HasBidCapacity()) RunPlayerPass(exploratory, Segments.ROLE_EXPLORE);
    }

    private void RunPlayerPass(string nation, string role)
    {
        RunTimedPass(Segments.Players(nation), role, () => BidOnSilverPlayers(nation));
    }

    private static HashSet<string> CoolingNations(Dictionary<string, SegmentRecord> records)
    {
        return NationRotation.NATIONS
            .Where(nation => records.TryGetValue(Segments.Players(nation), out var record) &&
                             SegmentHealth.Judge(record, DateTime.UtcNow, SEGMENT_MIN_RESOLVED_BIDS,
                                 SEGMENT_MIN_WIN_RATE, SEGMENT_COOLDOWN_HOURS) == SegmentVerdict.Cooling)
            .ToHashSet();
    }

    private void RunTimedPass(string segment, string role, Action pass)
    {
        var verdict = PrepareSegment(segment);

        if (verdict == SegmentVerdict.Cooling)
            Console.WriteLine($"Pass {segment}: skipped while cooling.");
        else
            RunPass(segment, role, pass);
    }

    private SegmentVerdict PrepareSegment(string segment)
    {
        var record = Database.GetSegmentRecords(SEGMENT_WINDOW_DAYS)
            .GetValueOrDefault(segment, new SegmentRecord(segment, 0, 0, 0, null));
        var verdict = SegmentHealth.Judge(record, DateTime.UtcNow, SEGMENT_MIN_RESOLVED_BIDS, SEGMENT_MIN_WIN_RATE,
            SEGMENT_COOLDOWN_HOURS);
        _segment = segment;
        _segmentBidsPlaced = 0;
        _segmentBidAllowance = SegmentHealth.BidAllowance(verdict, SEGMENT_PROBE_BIDS);
        _calibrationRatio = ReadCalibrationRatio(segment);

        Console.WriteLine(
            $"Segment {segment}: {record.Won} won, {record.Lost} lost, profit {record.Profit} over {SEGMENT_WINDOW_DAYS} days, verdict {verdict}.");

        return verdict;
    }

    private void RunPass(string segment, string role, Action pass)
    {
        var started = DateTime.UtcNow;
        var bidsBefore = _bidsPlaced;
        var readsBefore = _compareReads;
        _rowsConsidered = 0;
        _pacing = new PassPacing(PAGE_TURN_GAP_MS, COMPARE_READ_GAP_MS);
        _passStarted = started;

        Utility.Utility.RetryAction(pass, 2, 3000);

        var seconds = (int)(DateTime.UtcNow - started).TotalSeconds;
        Database.AddPass(segment, role, _rowsConsidered, _compareReads - readsBefore, _bidsPlaced - bidsBefore, seconds);
        Console.WriteLine(
            $"Pass {segment} ({role}): {_rowsConsidered} rows considered, {_compareReads - readsBefore} compare reads, {_bidsPlaced - bidsBefore} bids, {seconds} s.");
        LogPacing(segment, started, seconds);
    }

    private void LogPacing(string segment, DateTime started, int seconds)
    {
        var searches = _network.SearchTimes().Count(time => time >= started);
        var ended = _pacing.Ended ? ", pass ended early" : string.Empty;

        Console.WriteLine(
            $"Pacing {segment}: {_pacing.PagesTurned} page(s) in {seconds} s, {searches} searches, {_pacing.Waits} wait(s) totalling {_pacing.WaitMs / 1000} s, {_pacing.Backoffs} backoff(s){ended}.");
    }

    private void PaceSearch(int gapWaitMs)
    {
        CheckBackoff();

        var wait = Math.Max(gapWaitMs, SearchBudgetWaitMs());

        if (wait > 0) Pause(wait);
    }

    private void Pause(int milliseconds)
    {
        _pacing.RecordWait(milliseconds);
        Thread.Sleep(milliseconds);
    }

    private int SearchBudgetWaitMs()
    {
        var seconds = SearchBudget.WaitSeconds(_network.SearchTimes(), DateTime.UtcNow, SEARCHES_PER_MINUTE,
            SEARCHES_PER_HOUR);

        if (seconds > 0) Console.WriteLine($"Search budget reached; waiting {seconds} s for a slot.");

        return seconds * 1000;
    }

    private int PageTurnJitterMs()
    {
        return _random.Next(PAGE_TURN_JITTER_MIN_MS, PAGE_TURN_JITTER_MAX_MS + 1);
    }

    private void CheckBackoff()
    {
        var statuses = _network.FailuresSince(_passStarted).Select(failure => failure.Status).ToList();
        var action = Backoff.Decide(statuses);

        if (action == BackoffAction.StopRun) StopRun(statuses);
        else if (action == BackoffAction.EndPass && !_pacing.Ended) EndPass(statuses);
        else if (action == BackoffAction.SlowDown && _pacing.Multiplier == 1) SlowDown(statuses);
    }

    private void SlowDown(List<int> statuses)
    {
        Console.WriteLine(
            $"Backoff: statuses [{string.Join(", ", statuses)}] this pass; pausing {BACKOFF_PAUSE_SECONDS} s and doubling the pacing gaps.");
        Database.AddSnipeEvent("pacing", "backoff", (uint)statuses.Last(Backoff.THROTTLE_STATUSES.Contains), null, null, null,
            $"{_segment}: {string.Join(",", statuses)}");
        _pacing.SlowDown();
        Pause(BACKOFF_PAUSE_SECONDS * 1000);
    }

    private void EndPass(List<int> statuses)
    {
        Console.WriteLine($"Backoff: second throttle in statuses [{string.Join(", ", statuses)}]; ending the pass.");
        Database.AddSnipeEvent("pacing", "pass-ended", (uint)statuses.Last(Backoff.THROTTLE_STATUSES.Contains), null, null, null,
            $"{_segment}: {string.Join(",", statuses)}");
        _pacing.End();
    }

    private void StopRun(List<int> statuses)
    {
        var fatal = statuses.First(Backoff.FATAL_STATUSES.Contains);
        var reason = $"status {fatal} in [{string.Join(", ", statuses)}] means the market is locked or a captcha is due";

        Console.WriteLine($"Stopping the run: {reason}.");
        Database.AddSnipeEvent("pacing", "run-stopped", (uint)fatal, null, null, null, $"{_segment}: {reason}");

        throw new RunStoppedException(reason);
    }

    private static double ReadCalibrationRatio(string segment)
    {
        var sales = Database.GetSegmentSaleRatios(segment, CALIBRATION_WINDOW_DAYS);
        var ratio = SalesFeedback.CalibrationRatio(sales, CALIBRATION_PRIOR_WEIGHT, CALIBRATION_MIN_RATIO,
            CALIBRATION_MAX_RATIO);

        Console.WriteLine($"Segment {segment}: calibration ratio {ratio:F3} from {sales.Count} sale(s).");

        return ratio;
    }

    #endregion

    #region Sub-Routines

    private void ClearItemsFromTransferTargets()
    {
        GoToTransfers();
        GoToTransferTargets();
        MarkLostTargets();
        ClearNotWonItemsFromTransferTargets();
        SendWonItemsToTransferListFromTransferTargets();
    }

    private void ClearSoldItemsFromTransferList()
    {
        GoToTransfers();
        GoToTransferList();
        RecordAndClearSoldItems();
    }

    #endregion

    #region Market Search

    private void BidOnItems(string itemType, ElementKeys itemMarketElement)
    {
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var directoryPath = $@"{baseDirectory}/Configuration/Filters/Active{itemType}{_user}";

        if (Directory.Exists(directoryPath))
            foreach (var filterFile in Directory.GetFiles(directoryPath, "*.json"))
                BidWithFilter(filterFile, itemMarketElement);
        else
            Console.WriteLine($"Directory {directoryPath} does not exist.");
    }

    private void BidWithFilter(string filterFile, ElementKeys itemMarketElement)
    {
        BidWithFilter(JsonSerializer.Deserialize<Filter>(Utility.Utility.ReadJson(filterFile)), itemMarketElement);
    }

    private void BidWithFilter(Filter filterData, ElementKeys itemMarketElement)
    {
        SearchWithFilter(filterData, itemMarketElement);
        BidAcrossResultPages(filterData.MaxBidPrice);
    }

    private void SearchWithFilter(Filter filterData, ElementKeys itemMarketElement)
    {
        GoToTransfers();
        GoToTransferMarket();
        RequireClick(itemMarketElement);
        RequireClick(ElementKeys.RESET);

        if (filterData.Quality != null) SelectDropdownOption(ElementKeys.QUALITY_DROPDOWN, filterData.Quality);
        if (filterData.Nationality != null)
            SelectDropdownOption(ElementKeys.NATIONALITY_DROPDOWN, filterData.Nationality);
        if (filterData.Rarity != null) SelectDropdownOption(ElementKeys.RARITY_DROPDOWN, filterData.Rarity);

        SetSearchPrice(ElementKeys.MAX_BID_PRICE_INPUT, filterData.MaxBidPrice);
        SetSearchPrice(ElementKeys.MIN_BUY_NOW_PRICE_INPUT, filterData.MinBuyPrice);
        Search();
    }

    private void BidOnSilverPlayers(string nation)
    {
        Filter filter = new()
        {
            Quality = PLAYER_QUALITY,
            Nationality = nation,
            MaxBidPrice = PLAYER_MAX_BID,
            MinBuyPrice = PLAYER_MIN_BUY_NOW
        };

        BidWithFilter(filter, ElementKeys.PLAYER_ITEMS_TRANSFER_MARKET);
    }

    private void BidOnManagerItems()
    {
        BidOnItems("Manager", ElementKeys.MANAGER_ITEMS_TRANSFER_MARKET);
    }

    private void BidOnSilverClubItems(bool badges)
    {
        GoToTransfers();
        GoToTransferMarket();
        RequireClick(ElementKeys.RESET);
        RequireClick(ElementKeys.CLUB_ITEMS_TRANSFER_MARKET);
        SelectDropdownOption(ElementKeys.QUALITY_DROPDOWN, ElementKeys.QUALITY_DROPDOWN_SILVER);
        SelectDropdownOption(ElementKeys.CLUB_ITEMS_TYPE_DROPDOWN,
            badges ? ElementKeys.CLUB_ITEMS_TYPE_DROPDOWN_BADGES : ElementKeys.CLUB_ITEMS_TYPE_DROPDOWN_KITS);
        SetSearchPrice(ElementKeys.MAX_BID_PRICE_INPUT, CLUB_ITEM_MAX_BID);
        SetSearchPrice(ElementKeys.MIN_BUY_NOW_PRICE_INPUT, CLUB_ITEM_MIN_BUY_NOW);
        Search();
        BidAcrossResultPages(CLUB_ITEM_MAX_BID);
    }

    private void BidAcrossResultPages(uint maxBidCap)
    {
        var started = DateTime.UtcNow;
        var page = 0;
        var morePages = true;

        while (morePages && page < MAX_RESULT_PAGES && CanPlaceMoreBids() && WithinScanBudget(started))
        {
            page++;
            ProcessCandidates((row, info) => TryBidOnSelectedItem(row, info, maxBidCap), () => CanPlaceMoreBids() && WithinScanBudget(started));
            morePages = CanPlaceMoreBids() && !PageIsBeyondWindow() && GoToNextResultsPage();
        }

        Console.WriteLine($"Scanned {page} result page(s) in {(int)(DateTime.UtcNow - started).TotalSeconds} s.");
    }

    private static bool WithinScanBudget(DateTime started)
    {
        return Capacity.WithinBudget(started, DateTime.UtcNow, SCAN_BUDGET_SECONDS);
    }

    private RowFacts ReadRowFacts(RowSnapshot row)
    {
        RowFacts result = new(row.Classes, row.MinutesLeft, false, null, null, false);

        if (RowTriage.IsCandidate(result, MIN_AUCTION_MINUTES, MAX_AUCTION_MINUTES, MARGIN_COINS))
            result = ReadCachedRowFacts(row);

        return result;
    }

    private RowFacts ReadCachedRowFacts(RowSnapshot row)
    {
        var info = row.Key;
        var sightings = Database.GetRecentSightings(info, RESALE_WINDOW_DAYS);
        uint? cached = sightings.Count > 0 ? SalesFeedback.Calibrate(sightings[0].price, _calibrationRatio) : null;
        var hasSales = Database.GetRecentSales(info, RESALE_WINDOW_DAYS).Count > 0;

        return new RowFacts(row.Classes, row.MinutesLeft, _bidNamesThisRun.Contains(info), row.BidValue, cached,
            hasSales);
    }

    private bool PageIsBeyondWindow()
    {
        var snapshot = _screen.Snapshot(ElementKeys.RESULT_ROWS, false);
        var result = false;

        if (snapshot.Count > 0)
        {
            var minutes = snapshot[^1].MinutesLeft;
            result = minutes.HasValue && minutes.Value > MAX_AUCTION_MINUTES;
        }

        return result;
    }

    private bool GoToNextResultsPage()
    {
        PaceSearch(_pacing.PageTurnWaitMs(DateTime.UtcNow, PageTurnJitterMs()));

        var moved = _screen.TryClick(ElementKeys.RESULTS_NEXT, ShortWait);

        if (moved) _pacing.RecordPageTurn(DateTime.UtcNow);
        if (moved) WaitForResultsToSettle();

        return moved;
    }

    private void WaitForResultsToSettle()
    {
        _screen.WaitHidden(ElementKeys.CLICK_SHIELD, ShortWait);
        _screen.WaitVisible(ElementKeys.AUCTION_ITEMS, ShortWait);
        Thread.Sleep(500);
    }

    private void SelectDropdownOption(ElementKeys dropdown, ElementKeys option)
    {
        RequireClick(dropdown);
        Thread.Sleep(150);
        RequireClick(option);
        Thread.Sleep(150);
    }

    private void SelectDropdownOption(ElementKeys dropdown, string optionText)
    {
        RequireClick(dropdown);
        Thread.Sleep(150);

        if (!_screen.Click(By.XPath($"//li[text()='{optionText}']"), StandardWait))
            throw new InvalidOperationException($"Dropdown option '{optionText}' was not found.");

        Thread.Sleep(150);
    }

    private void SetSearchPrice(ElementKeys input, uint value)
    {
        var actual = _screen.SetInputValue(input, value, ShortWait);

        if (actual != value)
            throw new InvalidOperationException($"{Elements[input].Item2} ended at {actual} instead of {value}.");
    }

    private void Search()
    {
        PaceSearch(0);
        RequireClick(ElementKeys.SEARCH);
        _pacing.RecordPageTurn(DateTime.UtcNow);
        RequireTitle("Search Results");
        WaitForResultsToSettle();
    }

    #endregion

    #region Bidding

    private void ProcessCandidates(Action<IWebElement, string> action, Func<bool> canContinue)
    {
        HashSet<string> done = new();
        var plan = PlanCandidates(done);
        var position = 0;
        var replans = 0;

        while (position < plan.Count && replans < 3 && canContinue())
        {
            var (index, key) = plan[position];

            if (TryProcessCandidate(index, key, action))
            {
                done.Add(key);
                position++;
            }
            else
            {
                replans++;
                plan = PlanCandidates(done);
                position = 0;
            }
        }
    }

    private List<(int index, string key)> PlanCandidates(ISet<string> done)
    {
        var snapshot = _screen.Snapshot(ElementKeys.RESULT_ROWS, false);
        var candidates = snapshot
            .Where(row => !done.Contains(row.Key) && RowTriage.IsCandidate(ReadRowFacts(row), MIN_AUCTION_MINUTES,
                MAX_AUCTION_MINUTES, MARGIN_COINS))
            .Select(row => (row.Index, row.Key))
            .ToList();

        Console.WriteLine($"Results page: {snapshot.Count} rows, {candidates.Count} candidates.");

        return candidates;
    }

    private bool TryProcessCandidate(int index, string key, Action<IWebElement, string> action)
    {
        var processed = false;

        try
        {
            var current = _screen.Snapshot(ElementKeys.RESULT_ROWS, false);
            var rows = _screen.FindAll(ElementKeys.RESULT_ROWS);
            var matches = index < current.Count && index < rows.Count && current[index].Key == key;
            var inWindow = matches && current[index].MinutesLeft.HasValue &&
                           Pricing.IsWithinBidWindow(current[index].MinutesLeft!.Value, MIN_AUCTION_MINUTES, MAX_AUCTION_MINUTES);

            if (inWindow && _screen.Click(rows[index], ShortWait))
            {
                _rowsConsidered++;
                WaitForSelectedRow(ElementKeys.RESULT_ROWS, index);
                action(rows[index], key);
            }

            processed = matches;
        }
        catch (StaleElementReferenceException)
        {
        }

        return processed;
    }

    private void WaitForSelectedRow(ElementKeys rows, int index)
    {
        var deadline = DateTime.UtcNow + ShortWait;
        var selected = RowIsSelected(rows, index);

        while (!selected && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(100);
            selected = RowIsSelected(rows, index);
        }
    }

    private bool RowIsSelected(ElementKeys rows, int index)
    {
        var snapshot = _screen.Snapshot(rows, false);

        return index < snapshot.Count && BidRow.IsSelected(snapshot[index].Classes);
    }

    private bool CanPlaceMoreBids()
    {
        return HasBidCapacity() && _segmentBidsPlaced < _segmentBidAllowance && !_pacing.Ended;
    }

    private bool HasBidCapacity()
    {
        return Capacity.CanBid(_total, MAX_TRANSFER_TARGETS, _listed, MAX_TRANSFER_LIST, _bidsPlaced, _maxBids);
    }

    private void TryBidOnSelectedItem(IWebElement row, string info, uint maxBidCap)
    {
        var bidInput = _screen.WaitVisible(ElementKeys.FIND_ALL_PRICE_INPUTS, ShortWait);

        if (bidInput != null && !_bidNamesThisRun.Contains(info))
        {
            var minimumBid = Utility.Utility.CommaSeperatedNumberToUInt(bidInput.GetAttribute("value") ?? "0");
            var requiredResale = Pricing.RequiredResale(minimumBid, MARGIN_COINS);
            var resaleEstimate = minimumBid > maxBidCap ? null : GetResaleEstimate(info, requiredResale);

            if (minimumBid > maxBidCap) Console.WriteLine($"{info}: minimum {minimumBid} is over the cap {maxBidCap}; no compare read.");
            if (resaleEstimate.HasValue)
                PlaceBidIfWorthwhile(row, info, bidInput, minimumBid, resaleEstimate.Value, maxBidCap);
        }
    }

    private uint? GetResaleEstimate(string info, uint requiredResale)
    {
        var sales = Database.GetRecentSales(info, RESALE_WINDOW_DAYS);
        var askEstimate = GetCalibratedAskEstimate(info, requiredResale);
        var result = SalesFeedback.Resale(sales, askEstimate, ITEM_SALES_MIN, SALES_MAX_UPLIFT);

        if (sales.Count > 0)
            Console.WriteLine(
                $"{info}: {sales.Count} sale(s), ask estimate {askEstimate?.ToString() ?? "none"}, resale {result}.");

        return result;
    }

    private uint? GetCalibratedAskEstimate(string info, uint requiredResale)
    {
        var sightings = Database.GetRecentSightings(info, RESALE_WINDOW_DAYS);
        var knownCheap = sightings.Count > 0 &&
                         SalesFeedback.Calibrate(sightings[0].price, _calibrationRatio) < requiredResale;
        var stale = sightings.Count == 0 ||
                    Pricing.IsStale(sightings[0].timestamp, DateTime.UtcNow, RESALE_MAX_AGE_HOURS);

        if (stale && !knownCheap && TryRecordLowestPrice(info))
            sightings = Database.GetRecentSightings(info, RESALE_WINDOW_DAYS);

        var askEstimate = Pricing.EstimateResale(sightings.Select(sighting => sighting.price), RESALE_SAMPLE_SIZE);

        return askEstimate.HasValue ? SalesFeedback.Calibrate(askEstimate.Value, _calibrationRatio) : null;
    }

    private bool TryRecordLowestPrice(string info, int maxPages = MAX_COMPARE_PAGES)
    {
        var recorded = false;

        PaceSearch(_pacing.CompareWaitMs(DateTime.UtcNow));

        var clicked = DateTime.UtcNow;
        _pacing.RecordCompareRead(clicked);
        var comparePriceList = _screen.Click(ElementKeys.COMPARE_PRICE, StandardWait)
            ? _screen.WaitVisible(ElementKeys.COMPARE_PRICE_LIST, StandardWait)
            : null;

        if (comparePriceList != null)
        {
            var (asks, pages) = CollectComparePrices(maxPages, clicked);
            _compareReads++;
            var resale = Pricing.ResaleFromAsks(asks, MIN_COMPARE_LISTINGS);
            recorded = resale > 0;
            _lastAskCount = asks.Count;

            Database.AddCompareRead(info, asks, pages);
            if (recorded) Database.AddToSeenTable(resale, info);

            _screen.Click(ElementKeys.COMPARE_PRICE_BACK_BUTTON, StandardWait);
            _screen.WaitHidden(ElementKeys.COMPARE_PRICE_LIST, ShortWait);
        }

        return recorded;
    }

    private void PlaceBidIfWorthwhile(IWebElement row, string info, IWebElement bidInput, uint minimumBid,
        uint resaleEstimate, uint maxBidCap)
    {
        var ceiling = Math.Min(Pricing.MaxBid(resaleEstimate, MARGIN_COINS), maxBidCap);
        var withinBudget = Pricing.FitsExposureLimit(_coinBalance, _coinsCommitted, minimumBid, MAX_EXPOSURE_SHARE);

        if (minimumBid <= ceiling && withinBudget)
            PlaceBid(info, bidInput, minimumBid, resaleEstimate, ReadBidContext(row, minimumBid));
    }

    private BidContext ReadBidContext(IWebElement row, uint minimumBid)
    {
        return new BidContext(
            minimumBid,
            ReadRowValue(row, "Bid"),
            ReadRowValue(row, "Buy Now:"),
            ReadRowMinutes(row),
            _lastAskCount);
    }

    private static uint? ReadRowValue(IWebElement row, string label)
    {
        var values = row.FindElements(By.XPath($".//span[@class='label' and normalize-space(text())='{label}']/following-sibling::span"));
        uint? result = null;

        if (values.Count > 0 && uint.TryParse(values[0].Text.Replace(",", ""), out var parsed)) result = parsed;

        return result;
    }

    private static uint? ReadRowMinutes(IWebElement row)
    {
        var timeElements = row.FindElements(By.CssSelector(Elements[ElementKeys.ITEM_TIME_REMAINING].Item1));

        return timeElements.Count > 0 ? Pricing.ParseMinutesRemaining(timeElements[0].Text) : null;
    }

    private void PlaceBid(string info, IWebElement bidInput, uint amount, uint resaleEstimate, BidContext context)
    {
        var typed = _screen.SetInputValue(bidInput, amount) == amount;
        var clicked = typed && _screen.Click(ElementKeys.MAKE_BID, ShortWait);

        if (clicked && WaitForBidRegistered())
            RecordBid(info, amount, resaleEstimate, context);
        else
            Console.WriteLine($"Bid on {info} at {amount} was not registered.");

        _screen.DismissDialog();
    }

    private bool WaitForBidRegistered()
    {
        var deadline = DateTime.UtcNow + ShortWait;
        var registered = SelectedRowIsRegistered();

        while (!registered && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(300);
            registered = SelectedRowIsRegistered();
        }

        return registered;
    }

    private bool SelectedRowIsRegistered()
    {
        var row = _screen.WaitVisible(ElementKeys.SELECTED_ITEM, TimeSpan.Zero);

        return row != null && BidRow.IsRegistered(row.GetAttribute("class") ?? string.Empty);
    }

    private void RecordBid(string info, uint amount, uint resaleEstimate, BidContext context)
    {
        Database.AddBid(info, amount, resaleEstimate, context, _segment);
        _bidNamesThisRun.Add(info);
        _coinsCommitted += amount;
        _total += 1;
        _bidsPlaced += 1;
        _segmentBidsPlaced += 1;
        Console.WriteLine($"Bid {amount} on {info} in {_segment} (resale estimate {resaleEstimate}, {context.MinutesLeft} min left).");
    }

    private static string GetItemInfo(IWebElement row)
    {
        var name = row.FindElement(By.CssSelector(Elements[ElementKeys.ITEM_NAME].Item1)).Text;
        var item = row.FindElements(By.CssSelector("div.entityContainer > div.item"));
        var classes = item.Count > 0 ? item[0].GetAttribute("class") ?? string.Empty : string.Empty;

        return ItemKey.Build(name, TextOf(row, "div.itemDesc"), classes, TextOf(row, "div.rating"),
            TextOf(row, "div.position"));
    }

    private static string TextOf(IWebElement row, string cssSelector)
    {
        var elements = row.FindElements(By.CssSelector(cssSelector));

        return elements.Count > 0 ? elements[0].Text : string.Empty;
    }

    private (List<uint> asks, int pages) CollectComparePrices(int maxPages, DateTime clicked)
    {
        List<uint> prices = [];
        var page = 0;
        var morePages = true;
        var captured = 0;

        while (morePages && page < maxPages)
        {
            page++;
            var list = _screen.WaitVisible(ElementKeys.COMPARE_PRICE_LIST, ShortWait);
            var asks = list == null ? null : ConfirmedCapturedAsks(clicked, list);

            if (asks != null) captured++;
            if (asks != null) prices.AddRange(asks);
            else if (list != null) prices.AddRange(ScrapeComparePage(list));

            clicked = DateTime.UtcNow;
            morePages = list != null && prices.Count < MIN_COMPARE_LISTINGS && page < maxPages && TurnComparePage();

            if (morePages && asks == null) Thread.Sleep(1000);
        }

        Console.WriteLine($"Compare price read {prices.Count} listings over {page} page(s), {captured} from responses.");

        return (prices, page);
    }

    private bool TurnComparePage()
    {
        var visible = _screen.IsVisible(ElementKeys.COMPARE_PRICE_NEXT);

        if (visible) PaceSearch(_pacing.CompareWaitMs(DateTime.UtcNow));

        var moved = visible && _screen.TryClick(ElementKeys.COMPARE_PRICE_NEXT, ShortWait);

        if (moved) _pacing.RecordCompareRead(DateTime.UtcNow);

        return moved;
    }

    private List<uint> ScrapeComparePage(IWebElement list)
    {
        WaitForFirstBuyNow();

        return ReadBuyNowPrices(list);
    }

    private IWebElement? WaitForFirstBuyNow()
    {
        return _screen.WaitVisible(
            By.XPath($"{Elements[ElementKeys.COMPARE_PRICE_LIST].Item1}//span[text()='Buy Now:']/following-sibling::span"),
            ShortWait);
    }

    private IReadOnlyList<uint>? ConfirmedCapturedAsks(DateTime since, IWebElement list)
    {
        var asks = CapturedAsks(since);
        var first = asks == null ? null : WaitForFirstBuyNow();
        var shown = first == null ? null : ParseShownCoins(first.Text);
        var confirmed = asks != null && shown.HasValue && asks[0] == shown.Value;

        if (asks != null && !confirmed)
            Console.WriteLine($"Captured compare page did not match the list (first ask {asks[0]}, shown {shown?.ToString() ?? "none"}); scraping instead.");

        return confirmed ? asks : null;
    }

    private static uint? ParseShownCoins(string text)
    {
        uint? result = null;

        if (uint.TryParse(text.Replace(",", ""), out var parsed)) result = parsed;

        return result;
    }

    private IReadOnlyList<uint>? CapturedAsks(DateTime since)
    {
        var deadline = DateTime.UtcNow + ShortWait;
        var capture = LatestSearchCapture(since);

        while (capture == null && _network.Enabled && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(100);
            capture = LatestSearchCapture(since);
        }

        var asks = capture == null ? null : UtasPayloads.Asks(capture.Body);

        if (capture != null && asks!.Count == 0)
            Console.WriteLine(
                $"Captured compare response gave no asks (status {capture.Status}, {capture.Body.Length} chars: {capture.Body[..Math.Min(120, capture.Body.Length)]}).");

        return asks is { Count: > 0 } ? asks : null;
    }

    private Capture? LatestSearchCapture(DateTime since)
    {
        return _network.Since(since, CaptureKind.Search)
            .Where(capture => capture.Url.Contains("definitionId=", StringComparison.Ordinal))
            .MaxBy(capture => capture.Time);
    }

    private static List<uint> ReadBuyNowPrices(IWebElement list)
    {
        IList<IWebElement> buyNowSpans = list.FindElements(By.XPath(".//span[text()='Buy Now:']"));

        return buyNowSpans
            .Select(span => span.FindElement(By.XPath("./following-sibling::span")).Text)
            .Select(Utility.Utility.CommaSeperatedNumberToUInt)
            .ToList();
    }

    #endregion

    #region Sniping

    private void RunSnipePass()
    {
        var nation = NationRotation.NextExploratory(NationRotation.SNIPE_NATIONS, Database.GetLastSnipeNation(),
            new HashSet<string>());

        if (nation != null) RunTimedPass(Segments.SNIPE, Segments.ROLE_SNIPE, () => SnipeSilverPlayers(nation));
        if (nation != null) ListWonItemsNow();
    }

    private void ListWonItemsNow()
    {
        var started = DateTime.UtcNow;

        Utility.Utility.RetryAction(ClearItemsFromTransferTargets);
        Utility.Utility.RetryAction(ListItemsFromTransferList);

        Console.WriteLine($"Listing after the snipe pass took {(DateTime.UtcNow - started).TotalSeconds:F0} s.");
    }

    private void SnipeSilverPlayers(string nation)
    {
        Dictionary<string, uint> estimates = new();
        Filter filter = new()
        {
            Quality = PLAYER_QUALITY,
            Nationality = nation,
            MaxBidPrice = PLAYER_MAX_BID,
            MinBuyPrice = PLAYER_MIN_BUY_NOW
        };

        _snipeEstimates = estimates;
        Database.AddSnipeEvent(nation, "search", null, null, null, null, _segment);
        Console.WriteLine($"Snipe {nation}: searching silver players.");
        SearchWithFilter(filter, ElementKeys.PLAYER_ITEMS_TRANSFER_MARKET);
        WatchAcrossResultPages(estimates);
        Console.WriteLine($"Snipe {nation}: watching {estimates.Count} item(s).");

        if (estimates.Count > 0) SnipeWatchedTargets(estimates);
    }

    private void WatchAcrossResultPages(Dictionary<string, uint> estimates)
    {
        var page = 0;
        var morePages = true;

        var started = DateTime.UtcNow;

        while (morePages && page < MAX_RESULT_PAGES && CanWatchMore(estimates) && WithinScanBudget(started))
        {
            page++;
            ProcessCandidates((row, info) => TryWatchSelectedItem(row, info, estimates), () => CanWatchMore(estimates) && WithinScanBudget(started));
            morePages = CanWatchMore(estimates) && !PageIsBeyondWindow() && GoToNextResultsPage();
        }

        Console.WriteLine($"Scanned {page} result page(s) in {(int)(DateTime.UtcNow - started).TotalSeconds} s.");
    }

    private bool CanWatchMore(Dictionary<string, uint> estimates)
    {
        return estimates.Count < SNIPE_BATCH_SIZE && HasBidCapacity() && !_pacing.Ended;
    }

    private void TryWatchSelectedItem(IWebElement row, string info, Dictionary<string, uint> estimates)
    {
        var bidInput = _screen.WaitVisible(ElementKeys.FIND_ALL_PRICE_INPUTS, ShortWait);

        if (bidInput != null && !estimates.ContainsKey(info))
        {
            var minimumBid = Utility.Utility.CommaSeperatedNumberToUInt(bidInput.GetAttribute("value") ?? "0");
            var estimate = minimumBid > SNIPE_MAX_BID
                ? null
                : GetResaleEstimate(info, Pricing.RequiredResale(minimumBid, MARGIN_COINS));

            if (estimate.HasValue && Snipe.ShouldWatch(estimate.Value, minimumBid, MARGIN_COINS, SNIPE_MAX_BID,
                    estimates.Count, SNIPE_BATCH_SIZE))
                WatchSelectedItem(row, info, minimumBid, estimate.Value, estimates);
            else
                Database.AddSnipeEvent(info, "skip", null, minimumBid, estimate, ReadTimeText(row),
                    minimumBid > SNIPE_MAX_BID ? "cap" : "margin");
        }
    }

    private void WatchSelectedItem(IWebElement row, string info, uint minimumBid, uint estimate,
        Dictionary<string, uint> estimates)
    {
        var timeText = ReadTimeText(row);

        if (TryWatch(info))
        {
            estimates[info] = estimate;
            _total += 1;
            Database.AddSnipeEvent(info, "watch", null, minimumBid, estimate, timeText, null);
            Console.WriteLine($"Watching {info} (resale estimate {estimate}, minimum {minimumBid}, {timeText}).");
            Thread.Sleep(WATCH_SETTLE_MS);
        }
        else
        {
            Console.WriteLine($"Could not watch {info}.");
        }
    }

    private bool TryWatch(string info)
    {
        var attempts = 0;
        var watched = false;

        while (!watched && attempts < 2)
        {
            attempts++;
            var sent = DateTime.UtcNow;
            watched = _screen.Click(ElementKeys.WATCH, ShortWait) && WatchAccepted(info, sent);
            if (!watched) Thread.Sleep(WATCH_RETRY_MS);
        }

        return watched;
    }

    private bool WatchAccepted(string info, DateTime sent)
    {
        var button = _screen.WaitEnabled(ElementKeys.UNWATCH, ShortWait);
        var response = _network.Since(sent, CaptureKind.Watch).FirstOrDefault();

        if (response != null && response.Status != 200) RecordRefusedWatch(info, response);

        return Snipe.WatchConfirmed(button != null, response?.Status);
    }

    private static void RecordRefusedWatch(string info, Capture response)
    {
        var body = response.Body.Length > 200 ? response.Body[..200] : response.Body;

        Database.AddSnipeEvent(info, "watch-refused", (uint)response.Status, null, null, null, body);
        Console.WriteLine($"Watch on {info} refused with {response.Status}: {body}");
    }

    private void SnipeWatchedTargets(Dictionary<string, uint> estimates)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMinutes(SNIPE_MAX_MINUTES);
        Dictionary<string, uint> standing = new();
        var liveCount = -1;
        var finished = false;

        GoToTransfers();
        GoToTransferTargets();
        _lastRefresh = DateTime.UtcNow;

        while (!finished && DateTime.UtcNow < deadline && CanPlaceMoreBids())
        {
            finished = SnipePoll(estimates, standing, ref liveCount);
            if (!finished) Thread.Sleep(SNIPE_POLL_MS);
        }

        Console.WriteLine(finished
            ? "Snipe batch finished: no watched item is still live."
            : "Snipe loop stopped at the time or bid limit.");
    }

    private bool SnipePoll(Dictionary<string, uint> estimates, Dictionary<string, uint> standing, ref int liveCount)
    {
        var finished = false;

        try
        {
            CheckBackoff();

            var started = DateTime.UtcNow;
            var snapshot = _screen.Snapshot(ElementKeys.TARGET_ROWS, true);
            var live = LiveWatchedRows(snapshot, estimates);
            var readMs = (int)(DateTime.UtcNow - started).TotalMilliseconds;
            finished = live.Count == 0;

            if (liveCount < 0) LogMissingWatchedItems(snapshot, estimates);

            if (live.Count != liveCount)
                Console.WriteLine(
                    $"Snipe poll: {live.Count} watched item(s) live, {standing.Count} with our bid, read in {readMs} ms, seconds left [{string.Join(", ", live.Select(entry => entry.facts.SecondsLeft?.ToString() ?? entry.row.Time))}].");

            liveCount = live.Count;

            var frozen = live.Where(entry => entry.facts.Frozen).Select(entry => entry.row.Key).ToList();
            var failedStatus = _network.Since(_lastRefresh, CaptureKind.TradeStatus).Any(capture => Snipe.StatusFreezesRows(capture.Status));

            if ((frozen.Count > 0 || failedStatus) && RefreshDue())
                RefreshTransferTargets(frozen.Count > 0 ? $"frozen [{string.Join("; ", frozen)}]" : "failed status refresh");
            else
                foreach (var (row, facts) in live.Where(_ => CanPlaceMoreBids())) SnipeRowIfDue(row, facts, standing);
        }
        catch (StaleElementReferenceException)
        {
            Console.WriteLine("Transfer targets changed while polling.");
        }

        return finished;
    }

    private void LogMissingWatchedItems(IReadOnlyList<RowSnapshot> snapshot, Dictionary<string, uint> estimates)
    {
        var present = snapshot.Select(row => row.Key).ToHashSet();
        var missing = estimates.Keys.Where(key => !present.Contains(key)).ToList();

        Console.WriteLine(
            $"Transfer targets hold {snapshot.Count} row(s): [{string.Join("; ", snapshot.Select(row => $"{row.Key}: {row.Classes.Replace("listFUTItem has-auction-data", string.Empty).Trim()} {row.Time}"))}].");

        foreach (var key in missing)
        {
            Console.WriteLine($"Watched item {key} is not on the transfer targets list.");
            Database.AddSnipeEvent(key, "missing", null, null, estimates[key], null, null);
        }
    }

    private static List<(RowSnapshot row, TargetFacts facts)> LiveWatchedRows(IReadOnlyList<RowSnapshot> snapshot,
        Dictionary<string, uint> estimates)
    {
        List<(RowSnapshot row, TargetFacts facts)> result = [];

        foreach (var row in snapshot)
        {
            var model = row.TrustedModel;

            if (Snipe.IsLive(row.Classes, model?.TradeState) && estimates.TryGetValue(row.Key, out var estimate))
                result.Add((row,
                    new TargetFacts(row.Classes, row.MinutesLeft, row.NextBid, estimate, model?.SecondsLeft,
                        model?.BidState, Snipe.IsFrozen(model?.AgeMs, model?.SecondsLeft))));
        }

        return result;
    }

    private void SnipeRowIfDue(RowSnapshot row, TargetFacts facts, Dictionary<string, uint> standing)
    {
        var shown = row.BidValue ?? 0;

        if (Snipe.IsOurs(facts) && shown > standing.GetValueOrDefault(row.Key))
            ConfirmUnrecordedBid(row, facts, standing);
        else if (Snipe.ShouldBid(facts, MARGIN_COINS, SNIPE_MAX_BID, SNIPE_AIM_SECONDS))
            SnipeRow(row, facts, standing);
    }

    private void ConfirmUnrecordedBid(RowSnapshot row, TargetFacts facts, Dictionary<string, uint> standing)
    {
        var amount = row.BidValue ?? 0;

        Console.WriteLine($"Found our bid of {amount} on {row.Key} that was not recorded; recording it.");
        RecordSnipeBid(row.Key, amount, facts.Estimate, standing, row.Time, "confirmed", SnapshotBidContext(row, amount));
    }

    private BidContext SnapshotBidContext(RowSnapshot row, uint minimumBid)
    {
        return new BidContext(minimumBid, row.BidValue, row.BuyNowValue, row.MinutesLeft, _lastAskCount);
    }

    private void SnipeRow(RowSnapshot row, TargetFacts facts, Dictionary<string, uint> standing)
    {
        var element = FindTargetRow(row.Index);

        if (standing.ContainsKey(row.Key))
            Database.AddSnipeEvent(row.Key, "outbid", row.BidValue, facts.MinimumBid, facts.Estimate, row.Time,
                facts.SecondsLeft?.ToString());

        if (element != null && _screen.Click(element, ShortWait))
        {
            var clicked = DateTime.UtcNow;
            WaitForSelectedRow(ElementKeys.TARGET_ROWS, row.Index);
            var bidInput = _screen.WaitVisible(ElementKeys.FIND_ALL_PRICE_INPUTS, ShortWait);

            if (bidInput != null) SnipeSelectedItem(row, bidInput, facts, standing, clicked);
        }
    }

    private IWebElement? FindTargetRow(int index)
    {
        var rows = _screen.FindAll(ElementKeys.TARGET_ROWS);

        return index < rows.Count ? rows[index] : null;
    }

    private void SnipeSelectedItem(RowSnapshot row, IWebElement bidInput, TargetFacts facts,
        Dictionary<string, uint> standing, DateTime clicked)
    {
        var minimumBid = Utility.Utility.CommaSeperatedNumberToUInt(bidInput.GetAttribute("value") ?? "0");
        var previous = standing.GetValueOrDefault(row.Key);
        var delta = minimumBid > previous ? minimumBid - previous : 0;
        var confirmed = facts with { MinimumBid = minimumBid };

        if (!Snipe.ShouldBid(confirmed, MARGIN_COINS, SNIPE_MAX_BID, SNIPE_AIM_SECONDS))
            RecordSnipeSkip(row.Key, minimumBid, facts.Estimate, row.Time, "margin");
        else if (!Pricing.FitsExposureLimit(_coinBalance, _coinsCommitted, delta, MAX_EXPOSURE_SHARE))
            RecordSnipeSkip(row.Key, minimumBid, facts.Estimate, row.Time, "budget");
        else
            PlaceSnipeBid(row.Key, bidInput, minimumBid, facts.Estimate, standing, row.Time,
                SnapshotBidContext(row, minimumBid), clicked, row.TrustedModel?.TradeId);
    }

    private void RecordSnipeSkip(string info, uint minimumBid, uint estimate, string timeText, string reason)
    {
        Database.AddSnipeEvent(info, "skip", null, minimumBid, estimate, timeText, reason);
        Console.WriteLine($"Skipping {info} at minimum {minimumBid} (resale estimate {estimate}, {timeText}): {reason}.");
    }

    private void PlaceSnipeBid(string info, IWebElement bidInput, uint amount, uint estimate,
        Dictionary<string, uint> standing, string timeText, BidContext context, DateTime rowClicked, string? tradeId)
    {
        var typed = _screen.SetInputValue(bidInput, amount) == amount;
        var sent = DateTime.UtcNow;
        var clicked = typed && _screen.Click(ElementKeys.MAKE_BID, ShortWait);
        var chain = $"chain {(int)(DateTime.UtcNow - rowClicked).TotalMilliseconds} ms";
        var (outcome, reason) = clicked ? WaitForSnipeOutcome(amount, tradeId, sent) : (BidOutcome.Failed, "click failed");
        var detail = $"{chain}, {reason}";

        if (outcome == BidOutcome.Registered)
            RecordSnipeBid(info, amount, estimate, standing, timeText, standing.ContainsKey(info) ? "rebid" : "bid", context, detail);
        else if (outcome == BidOutcome.Overtaken)
            RecordOvertakenBid(info, amount, estimate, timeText, detail);
        else
            RecoverFromUnregisteredBid(info, amount, estimate, timeText, $"{detail}, {DescribeSelectedRow(typed, clicked)}");

        _screen.DismissDialog();
    }

    private (BidOutcome outcome, string reason) WaitForSnipeOutcome(uint amount, string? tradeId, DateTime sent)
    {
        var deadline = DateTime.UtcNow + ShortWait;
        var result = ReadBidOutcome(amount, tradeId, sent);

        while (result.outcome == BidOutcome.Failed && !result.settled && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(200);
            result = ReadBidOutcome(amount, tradeId, sent);
        }

        return (result.outcome, result.reason);
    }

    private (BidOutcome outcome, string reason, bool settled) ReadBidOutcome(uint amount, string? tradeId, DateTime sent)
    {
        var response = tradeId == null
            ? null
            : _network.Since(sent, CaptureKind.Bid)
                .Select(capture => UtasPayloads.BidResult(capture.Status, capture.Body, tradeId))
                .FirstOrDefault(result => result != null);
        var fromRow = ReadSelectedRowOutcome(amount);

        return response != null
            ? (response.Outcome, $"server {response.Reason}", true)
            : (fromRow, fromRow == BidOutcome.Failed ? "no response seen" : "row state", false);
    }

    private BidOutcome ReadSelectedRowOutcome(uint amount)
    {
        var selected = _screen.Snapshot(ElementKeys.TARGET_ROWS, true)
            .FirstOrDefault(row => BidRow.IsSelected(row.Classes));

        return selected == null
            ? BidOutcome.Failed
            : Snipe.Outcome(selected.Classes, amount, selected.BidValue, selected.TrustedModel?.BidState);
    }

    private void RecordOvertakenBid(string info, uint amount, uint estimate, string timeText, string detail)
    {
        Database.AddSnipeEvent(info, "overtaken", amount, amount, estimate, timeText, detail);
        Console.WriteLine($"Bid on {info} at {amount} was overtaken before it showed; trying again next poll.");
    }

    private void RecordSnipeBid(string info, uint amount, uint estimate, Dictionary<string, uint> standing,
        string timeText, string eventName, BidContext context, string? detail = null)
    {
        var previous = standing.GetValueOrDefault(info);
        var first = !standing.ContainsKey(info);

        if (first) Database.AddBid(info, amount, estimate, context, _segment);
        else Database.RaiseOpenBid(info, amount);

        Database.AddSnipeEvent(info, eventName, amount, context.MinimumBid, estimate, timeText, detail);
        standing[info] = amount;
        _bidNamesThisRun.Add(info);
        _coinsCommitted += amount > previous ? amount - previous : 0;
        _bidsPlaced += first ? 1u : 0u;
        _segmentBidsPlaced += first ? 1u : 0u;
        Console.WriteLine($"Snipe {eventName} {amount} on {info} in {_segment} (resale estimate {estimate}, {timeText}).");
    }

    private string DescribeSelectedRow(bool typed, bool clicked)
    {
        var selected = _screen.Snapshot(ElementKeys.TARGET_ROWS, true).FirstOrDefault(row => BidRow.IsSelected(row.Classes));
        var state = selected == null
            ? "no selected row"
            : $"{selected.Key} {selected.Classes.Replace("listFUTItem has-auction-data", string.Empty).Trim()} bid {selected.Bid} {selected.Time} model {selected.TrustedModel?.BidState ?? "none"} {selected.TrustedModel?.SecondsLeft?.ToString() ?? "?"}s";

        return $"typed {typed}, clicked {clicked}, {state}";
    }

    private void RecoverFromUnregisteredBid(string info, uint amount, uint estimate, string timeText, string detail)
    {
        Console.WriteLine($"Bid on {info} at {amount} was not registered ({detail}); refreshing transfer targets.");
        Database.AddSnipeEvent(info, "unregistered", amount, amount, estimate, timeText, detail);
        _screen.DismissDialog();
        RefreshTransferTargets($"unregistered bid on {info}");
    }

    private bool RefreshDue()
    {
        return (DateTime.UtcNow - _lastRefresh).TotalSeconds >= REFRESH_GAP_SECONDS;
    }

    private void RefreshTransferTargets(string reason)
    {
        var dirtied = ClearExpiredTargets() || UnwatchSacrificialRow();
        var method = dirtied ? "dirty" : "reload";

        if (dirtied) GoToTransfers();
        if (dirtied) GoToTransferTargets();
        else ReloadWebApp();

        _lastRefresh = DateTime.UtcNow;
        Database.AddSnipeEvent("targets", "refresh", null, null, null, null, $"{method}: {reason}");
        Console.WriteLine($"Refreshed transfer targets ({method}): {reason}.");
    }

    private bool ClearExpiredTargets()
    {
        var visible = _screen.IsVisible(ElementKeys.CLEAR_NOT_WON_TRANSFER_TARGETS);

        if (visible) MarkExpiredTargets();

        return visible && _screen.TryClick(ElementKeys.CLEAR_NOT_WON_TRANSFER_TARGETS, ShortWait);
    }

    private void MarkExpiredTargets()
    {
        var expired = _screen.FindAll(ElementKeys.TARGET_ROWS)
            .Where(row => !Snipe.IsLive(row.GetAttribute("class") ?? string.Empty) &&
                          BidRow.IsLost(row.GetAttribute("class") ?? string.Empty))
            .ToList();

        foreach (var row in expired) MarkLostTarget(row);
    }

    private bool UnwatchSacrificialRow()
    {
        var live = LiveWatchedRows(_screen.Snapshot(ElementKeys.TARGET_ROWS, true), _snipeEstimates);
        var victim = live.FirstOrDefault(entry => Snipe.IsSacrificial(entry.facts, MARGIN_COINS, SNIPE_MAX_BID));
        var unwatched = victim.row != null && UnwatchRow(victim.row);

        if (unwatched)
            Database.AddSnipeEvent(victim.row!.Key, "unwatch", null, victim.facts.MinimumBid, victim.facts.Estimate,
                victim.row.Time, "sacrificed to refresh the list");
        if (unwatched) Console.WriteLine($"Unwatched {victim.row!.Key} to refresh the list.");

        return unwatched;
    }

    private bool UnwatchRow(RowSnapshot row)
    {
        var element = FindTargetRow(row.Index);
        var sent = DateTime.UtcNow;
        var clicked = element != null && _screen.Click(element, ShortWait) &&
                      _screen.Click(ElementKeys.UNWATCH, ShortWait);

        if (clicked) Thread.Sleep(500);

        var response = _network.Since(sent, CaptureKind.Unwatch).FirstOrDefault();

        return clicked && (response == null || response.Status == 200);
    }

    private void ReloadWebApp()
    {
        _screen.DismissDialog();
        _driver.Navigate().Refresh();
        Thread.Sleep(3000);
        EnsureLoggedIn();
        GoToTransfers();
        GoToTransferTargets();
    }

    private static string ReadTimeText(IWebElement row)
    {
        var timeElements = row.FindElements(By.CssSelector(Elements[ElementKeys.ITEM_TIME_REMAINING].Item1));

        return timeElements.Count > 0 ? timeElements[0].Text.Trim() : string.Empty;
    }

    #endregion

    #region Listing

    private void ListItemsFromTransferList()
    {
        Dictionary<string, uint> listPrices = new();
        var skipped = 0;
        var listedSinceRefresh = 0;
        var attempts = 0;
        var finished = false;

        GoToTransfers();
        GoToTransferList();

        while (!finished && attempts < MAX_LISTING_ATTEMPTS)
        {
            attempts++;
            Thread.Sleep(300);

            var candidates = _screen.FindAll(ElementKeys.LISTABLE_ITEMS);
            finished = candidates.Count <= skipped;

            if (!finished && TryListCandidate(candidates[skipped], listPrices))
                listedSinceRefresh++;
            else if (!finished)
                skipped++;

            if (listedSinceRefresh >= LISTINGS_BEFORE_REFRESH)
            {
                GoToTransfers();
                GoToTransferList();
                listedSinceRefresh = 0;
            }
        }
    }

    private bool TryListCandidate(IWebElement row, Dictionary<string, uint> listPrices)
    {
        var listed = false;

        if (_screen.Click(row, ShortWait))
        {
            Thread.Sleep(500);
            var info = GetItemInfo(row);

            if (!listPrices.TryGetValue(info, out var marketResale))
            {
                marketResale = DetermineMarketResale(info);
                listPrices[info] = marketResale;
            }

            listed = marketResale > 0 && ListSelectedItem(info, marketResale);
        }

        return listed;
    }

    private uint DetermineMarketResale(string info)
    {
        uint result = 0;

        if (TryRecordLowestPrice(info, MAX_COMPARE_PAGES_FOR_LISTING))
        {
            var sightings = Database.GetRecentSightings(info, RESALE_WINDOW_DAYS);
            result = sightings[0].price;
        }

        return result;
    }

    private uint? ReadCost(string info)
    {
        var boughtFor = ReadBoughtFor();
        var lastBid = Database.GetLatestBidPrice(info, RESALE_WINDOW_DAYS);
        uint? result = boughtFor ?? lastBid;

        if (boughtFor.HasValue && lastBid.HasValue) result = Math.Max(boughtFor.Value, lastBid.Value);

        return result;
    }

    private uint? ReadBoughtFor()
    {
        var values = _screen.FindAll(ElementKeys.BOUGHT_FOR_VALUE);
        uint? result = null;

        if (values.Count > 0 && uint.TryParse(values[0].Text.Replace(",", ""), out var parsed)) result = parsed;

        return result;
    }

    private bool ListSelectedItem(string info, uint marketResale)
    {
        var cost = ReadCost(info);
        var (startPrice, buyNow) = Pricing.ListingPrices(marketResale, cost);
        var listed = _screen.Click(ElementKeys.LIST_ITEM_PRE_PRICE, StandardWait)
                     && _screen.SetInputValue(ElementKeys.MIN_PRICE_LIST_ITEM, startPrice, ShortWait) != null
                     && _screen.SetInputValue(ElementKeys.MAX_PRICE_LIST_ITEM, buyNow, ShortWait) != null
                     && _screen.Click(ElementKeys.LIST_ITEM, StandardWait);

        Console.WriteLine($"Listing {info}: market {marketResale}, cost {cost?.ToString() ?? "unknown"}, start {startPrice}, buy now {buyNow}, listed {listed}.");
        if (listed) Database.MarkLatestBidListed(info, buyNow);
        Thread.Sleep(1000);

        return listed;
    }

    #endregion

    #region Transfer Targets And Sold Items

    private void MarkLostTargets()
    {
        var lostRows = _screen.FindAll(ElementKeys.TARGET_ROWS)
            .Where(row => BidRow.IsLost(row.GetAttribute("class") ?? string.Empty))
            .ToList();
        var marked = 0;

        foreach (var row in lostRows) marked += MarkLostTarget(row);

        Console.WriteLine($"Transfer targets: {lostRows.Count} expired or outbid rows, {marked} names read.");
    }

    private static int MarkLostTarget(IWebElement row)
    {
        var result = 0;

        try
        {
            Database.MarkLatestOpenBidLost(GetItemInfo(row));
            result = 1;
        }
        catch (WebDriverException exception)
        {
            Console.WriteLine($"Could not read a lost target row: {exception.Message}");
        }

        return result;
    }

    private void ClearNotWonItemsFromTransferTargets()
    {
        if (_screen.IsVisible(ElementKeys.CLEAR_NOT_WON_TRANSFER_TARGETS))
            _screen.TryClick(ElementKeys.CLEAR_NOT_WON_TRANSFER_TARGETS, ShortWait);
        Database.MarkStaleOpenBidsLost(OPEN_BID_TIMEOUT_HOURS);
    }

    private void SendWonItemsToTransferListFromTransferTargets()
    {
        var attempts = 0;
        var remaining = _screen.FindAll(ElementKeys.WON_TARGET).Count;

        while (remaining > 0 && attempts < MAX_WON_ITEM_ATTEMPTS)
        {
            attempts++;
            var rows = _screen.FindAll(ElementKeys.WON_TARGET);

            if (rows.Count > 0) SendWonItemToTransferList(rows[0]);

            remaining = _screen.FindAll(ElementKeys.WON_TARGET).Count;
        }
    }

    private void SendWonItemToTransferList(IWebElement row)
    {
        if (_screen.Click(row, ShortWait))
        {
            Thread.Sleep(1000);
            Database.MarkLatestOpenBidWon(GetItemInfo(row));
            _screen.Click(ElementKeys.SEND_TO_TRANSFER_LIST, StandardWait);
            Thread.Sleep(1000);
        }
    }

    private void RecordAndClearSoldItems()
    {
        var soldCount = _screen.FindAll(ElementKeys.WON_TARGET).Count;

        for (var index = 0; index < soldCount; index++)
        {
            var rows = _screen.FindAll(ElementKeys.WON_TARGET);

            if (index < rows.Count) RecordSoldItem(rows[index]);
        }

        if (soldCount > 0) _screen.Click(ElementKeys.CLEAR_SOLD_TRANSFERS, StandardWait);
    }

    private void RecordSoldItem(IWebElement row)
    {
        if (_screen.Click(row, ShortWait))
        {
            Thread.Sleep(500);
            var info = GetItemInfo(row);
            var soldPrice = ReadSoldPrice(row);

            Database.AddToSoldTable(soldPrice, info);
            Database.MarkLatestWonBidSold(info, soldPrice);
        }
    }

    private static uint ReadSoldPrice(IWebElement row)
    {
        var soldForValues = row.FindElements(By.XPath(".//span[starts-with(normalize-space(text()), 'Sold For')]/following-sibling::span"));
        IList<IWebElement> priceElements = row.FindElements(By.CssSelector("span.currency-coins.value"));
        var soldPriceString = soldForValues.Count > 0
            ? soldForValues[0].Text
            : priceElements.Count > 0 ? priceElements[^1].Text : "0";

        return Utility.Utility.CommaSeperatedNumberToUInt(soldPriceString);
    }

    #endregion

    #region Navigation

    private void RequireClick(ElementKeys key)
    {
        if (!_screen.Click(key, StandardWait))
            throw new InvalidOperationException($"Could not click {Elements[key].Item2} on '{_screen.ReadTitle()}'.");
    }

    private void RequireTitle(string title)
    {
        if (!_screen.WaitForTitle(title, StandardWait))
            throw new InvalidOperationException($"Expected screen '{title}' but saw '{_screen.ReadTitle()}'.");
    }

    private void GoToTransfers()
    {
        if (!_screen.IsVisible(ElementKeys.NAVIGATION_BAR)) EnsureLoggedIn();
        RequireClick(ElementKeys.LEFT_HAND_PANE_TRANSFERS);
        RequireTitle("Transfers");

        var totalText = _screen.ReadText(ElementKeys.TRANSFER_TARGETS_TOTAL, ShortWait);
        var listedText = _screen.ReadText(ElementKeys.TRANSFER_LIST_TOTAL, ShortWait);

        if (totalText.Length > 0) _total = Utility.Utility.CommaSeperatedNumberToUInt(totalText);
        if (listedText.Length > 0) _listed = Utility.Utility.CommaSeperatedNumberToUInt(listedText);
    }

    private void GoToTransferList()
    {
        RequireClick(ElementKeys.TRANSFER_LIST);
        RequireTitle("Transfer List");
    }

    private void GoToTransferTargets()
    {
        RequireClick(ElementKeys.TRANSFER_TARGETS);
        RequireTitle("Transfer Targets");
    }

    private void GoToTransferMarket()
    {
        RequireClick(ElementKeys.TRANSFER_MARKET);
        RequireTitle("Search the Transfer Market");
    }

    private void GetCoinTotal()
    {
        var coinTotalAsString = _screen.ReadText(ElementKeys.COIN_TOTAL, StandardWait);

        if (coinTotalAsString.Length == 0) throw new InvalidOperationException("Coin total was not visible.");

        var coinTotal = Utility.Utility.CommaSeperatedNumberToUInt(coinTotalAsString);
        _coinBalance = coinTotal;

        Database.AddToCoinTable(coinTotal);
    }

    #endregion
}
