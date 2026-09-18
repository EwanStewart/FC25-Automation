using Automation.Flow;
using Automation.Setup;
using Automation.Trading;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.Text.Json;
using static Automation.Definitions.Fc25Definitions;
using static Automation.Trading.BiddingStrategy;

namespace Automation;

public class Fc25
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
    private readonly string _user;
    private readonly uint _maxBids;
    private readonly Dictionary<string, int> _credentialAttempts = new();
    private uint _total;
    private uint _bidsPlaced;
    private uint _coinBalance;
    private uint _coinsCommitted;

    #region Constructor

    public Fc25(string configuration, bool smokeTest)
    {
        _user = configuration;
        _maxBids = smokeTest ? 1u : uint.MaxValue;
        Browser browser = new(configuration);
        _driver = browser.Chrome;
        _screen = new Screen(_driver);

        try
        {
            EnsureLoggedIn();
            GetCoinTotal();

            if (smokeTest)
                SmokeTestRoutine();
            else
                ListAndBidRoutine();
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
        }
    }

    #endregion

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
            _screen.IsVisible(ElementKeys.UNSUPPORTED_BROWSER));
    }

    private void PerformLoginStep(LoginStep step)
    {
        switch (step)
        {
            case LoginStep.ClickLogin:
                _screen.Click(ElementKeys.INITIAL_LOGIN, ShortWait);
                break;
            case LoginStep.EnterEmail:
                SubmitCredential(ElementKeys.EMAIL_INPUT, "FC_EMAIL");
                break;
            case LoginStep.EnterPassword:
                SubmitCredential(ElementKeys.PASSWORD_INPUT, "FC_PASSWORD");
                break;
            case LoginStep.Continue:
                _screen.Click(ElementKeys.CONTINUE, ShortWait);
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
        MaintainTransfers();
        RunClubItemBidPass(false);

        if (_bidsPlaced == 0) RunClubItemBidPass(true);
    }

    private void MaintainTransfers()
    {
        Utility.Utility.RetryAction(ClearItemsFromTransferTargets);
        Utility.Utility.RetryAction(ClearSoldItemsFromTransferList);
        Utility.Utility.RetryAction(ListItemsFromTransferList);
    }

    private void ListAndBidRoutine()
    {
        MaintainTransfers();

        RunClubItemBidPass(false);

        if (CanPlaceMoreBids()) RunClubItemBidPass(true);

        Utility.Utility.RetryAction(() => BidOnPlayerItems());
    }

    private void RunClubItemBidPass(bool badges)
    {
        Utility.Utility.RetryAction(() => BidOnSilverClubItems(badges), 2, 3000);
    }

    #endregion

    #region Sub-Routines

    private void ClearItemsFromTransferTargets()
    {
        GoToTransfers();
        GoToTransferTargets();
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
        var filterData = JsonSerializer.Deserialize<Filter>(Utility.Utility.ReadJson(filterFile));

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
        BidAcrossResultPages(filterData.MaxBidPrice);
    }

    private void BidOnPlayerItems()
    {
        BidOnItems("Player", ElementKeys.PLAYER_ITEMS_TRANSFER_MARKET);
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
        var page = 0;
        var morePages = true;

        while (morePages && page < RESULT_PAGES_TO_SCAN && CanPlaceMoreBids())
        {
            page++;
            BidOnAuctionItems(maxBidCap);
            morePages = CanPlaceMoreBids() && page < RESULT_PAGES_TO_SCAN && GoToNextResultsPage();
        }
    }

    private bool GoToNextResultsPage()
    {
        var moved = _screen.IsVisible(ElementKeys.RESULTS_NEXT) && _screen.Click(ElementKeys.RESULTS_NEXT, ShortWait);

        if (moved) Thread.Sleep(1500);

        return moved;
    }

    private void SelectDropdownOption(ElementKeys dropdown, ElementKeys option)
    {
        RequireClick(dropdown);
        Thread.Sleep(300);
        RequireClick(option);
        Thread.Sleep(300);
    }

    private void SelectDropdownOption(ElementKeys dropdown, string optionText)
    {
        RequireClick(dropdown);
        Thread.Sleep(300);

        if (!_screen.Click(By.XPath($"//li[text()='{optionText}']"), StandardWait))
            throw new InvalidOperationException($"Dropdown option '{optionText}' was not found.");

        Thread.Sleep(300);
    }

    private void SetSearchPrice(ElementKeys input, uint value)
    {
        var actual = _screen.SetInputValue(input, value, ShortWait);

        if (actual != value)
            throw new InvalidOperationException($"{Elements[input].Item2} ended at {actual} instead of {value}.");
    }

    private void Search()
    {
        RequireClick(ElementKeys.SEARCH);
        RequireTitle("Search Results");
        Thread.Sleep(1000);
    }

    #endregion

    #region Bidding

    private void BidOnAuctionItems(uint maxBidCap)
    {
        var rows = _screen.FindAll(ElementKeys.AUCTION_ITEMS);
        var index = 0;
        var listIsCurrent = true;

        while (index < rows.Count && listIsCurrent && CanPlaceMoreBids())
        {
            listIsCurrent = TryProcessRow(rows[index], maxBidCap);
            index++;
        }
    }

    private bool CanPlaceMoreBids()
    {
        return _total < MAX_TRANSFER_TARGETS && _bidsPlaced < _maxBids;
    }

    private bool TryProcessRow(IWebElement row, uint maxBidCap)
    {
        var processed = true;

        try
        {
            if (ShouldConsiderRow(row) && _screen.Click(row, ShortWait))
            {
                Thread.Sleep(500);
                TryBidOnSelectedItem(row, maxBidCap);
            }
        }
        catch (StaleElementReferenceException)
        {
            processed = false;
        }

        return processed;
    }

    private static bool ShouldConsiderRow(IWebElement row)
    {
        return !BidRow.IsOurs(row.GetAttribute("class") ?? string.Empty) && IsWithinBidWindow(row);
    }

    private static bool IsWithinBidWindow(IWebElement row)
    {
        var result = false;
        var timeElements = row.FindElements(By.CssSelector(Elements[ElementKeys.ITEM_TIME_REMAINING].Item1));

        if (timeElements.Count > 0)
        {
            var minutes = Pricing.ParseMinutesRemaining(timeElements[0].Text);
            result = minutes.HasValue &&
                     Pricing.IsWithinBidWindow(minutes.Value, MIN_AUCTION_MINUTES, MAX_AUCTION_MINUTES);
        }

        return result;
    }

    private void TryBidOnSelectedItem(IWebElement row, uint maxBidCap)
    {
        var info = GetItemInfo(row);
        var resaleEstimate = GetResaleEstimate(info);

        if (resaleEstimate.HasValue) PlaceBidIfWorthwhile(info, resaleEstimate.Value, maxBidCap);
    }

    private uint? GetResaleEstimate(string info)
    {
        var sightings = Database.GetRecentSightings(info, RESALE_WINDOW_DAYS);
        var needsRefresh = sightings.Count == 0 ||
                           Pricing.IsStale(sightings[0].timestamp, DateTime.UtcNow, RESALE_MAX_AGE_HOURS);

        if (needsRefresh && TryRecordLowestPrice(info))
            sightings = Database.GetRecentSightings(info, RESALE_WINDOW_DAYS);

        return Pricing.EstimateResale(sightings.Select(sighting => sighting.price), RESALE_SAMPLE_SIZE);
    }

    private bool TryRecordLowestPrice(string info)
    {
        var recorded = false;
        var comparePriceList = _screen.Click(ElementKeys.COMPARE_PRICE, StandardWait)
            ? _screen.WaitVisible(ElementKeys.COMPARE_PRICE_LIST, StandardWait)
            : null;

        if (comparePriceList != null)
        {
            Thread.Sleep(1000);
            var lowestPrice = Pricing.LowestBuyNow(CollectComparePrices(), MIN_COMPARE_LISTINGS);
            recorded = lowestPrice > 0;

            if (recorded) Database.AddToSeenTable(lowestPrice, info);

            _screen.Click(ElementKeys.COMPARE_PRICE_BACK_BUTTON, StandardWait);
            _screen.WaitHidden(ElementKeys.COMPARE_PRICE_LIST, ShortWait);
        }

        return recorded;
    }

    private void PlaceBidIfWorthwhile(string info, uint resaleEstimate, uint maxBidCap)
    {
        var ceiling = Math.Min(Pricing.MaxBid(resaleEstimate, MARGIN_COINS), maxBidCap);
        var bidInput = _screen.WaitVisible(ElementKeys.FIND_ALL_PRICE_INPUTS, ShortWait);
        var minimumBid = bidInput == null
            ? uint.MaxValue
            : Utility.Utility.CommaSeperatedNumberToUInt(bidInput.GetAttribute("value") ?? "0");
        var withinBudget = Pricing.FitsExposureLimit(_coinBalance, _coinsCommitted, ceiling, MAX_EXPOSURE_SHARE);

        if (bidInput != null && minimumBid <= ceiling && withinBudget)
            PlaceBid(info, bidInput, ceiling, resaleEstimate);
    }

    private void PlaceBid(string info, IWebElement bidInput, uint amount, uint resaleEstimate)
    {
        var typed = _screen.SetInputValue(bidInput, amount) == amount;
        var clicked = typed && _screen.Click(ElementKeys.MAKE_BID, ShortWait);

        if (clicked && WaitForBidRegistered())
            RecordBid(info, amount, resaleEstimate);
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

    private void RecordBid(string info, uint amount, uint resaleEstimate)
    {
        Database.AddBid(info, amount, resaleEstimate);
        _coinsCommitted += amount;
        _total += 1;
        _bidsPlaced += 1;
        Console.WriteLine($"Bid {amount} on {info} (resale estimate {resaleEstimate}).");
    }

    private string GetItemInfo(IWebElement row)
    {
        var name = row.FindElement(By.CssSelector(Elements[ElementKeys.ITEM_NAME].Item1)).Text;
        var type = string.Empty;

        try
        {
            var typeParent = _driver.FindElement(By.CssSelector("div.tns-item.tns-slide-active"));
            type = typeParent.FindElement(By.CssSelector("div.clubView")).Text;
        }
        catch (NoSuchElementException)
        {
        }

        return $"{name} {type}";
    }

    private List<uint> CollectComparePrices()
    {
        List<uint> prices = [];
        var page = 0;
        var morePages = true;

        while (morePages && page < MAX_COMPARE_PAGES)
        {
            page++;
            var list = _screen.WaitVisible(ElementKeys.COMPARE_PRICE_LIST, ShortWait);

            if (list != null) prices.AddRange(ReadBuyNowPrices(list));

            morePages = list != null && _screen.IsVisible(ElementKeys.COMPARE_PRICE_NEXT) &&
                        _screen.Click(ElementKeys.COMPARE_PRICE_NEXT, ShortWait);

            if (morePages) Thread.Sleep(1500);
        }

        Console.WriteLine($"Compare price read {prices.Count} listings over {page} page(s).");

        return prices;
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
            Thread.Sleep(1000);

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

            if (!listPrices.TryGetValue(info, out var listPrice))
            {
                listPrice = DetermineListingPrice(info);
                listPrices[info] = listPrice;
            }

            listed = ShouldList(info, listPrice) && ListSelectedItem(listPrice);
        }

        return listed;
    }

    private uint DetermineListingPrice(string info)
    {
        uint result = 0;

        if (TryRecordLowestPrice(info))
        {
            var sightings = Database.GetRecentSightings(info, RESALE_WINDOW_DAYS);
            result = Pricing.ListingPrice(sightings[0].price);
        }

        return result;
    }

    private static bool ShouldList(string info, uint listPrice)
    {
        var paid = Database.GetLatestWonBidPrice(info);
        var result = listPrice > 0 && (!paid.HasValue || Pricing.IsProfitable(listPrice, paid.Value));

        if (!result) Console.WriteLine($"Not listing {info} at {listPrice}.");

        return result;
    }

    private bool ListSelectedItem(uint listPrice)
    {
        var startPrice = Pricing.ListingPrice(listPrice);
        var listed = _screen.Click(ElementKeys.LIST_ITEM_PRE_PRICE, StandardWait)
                     && _screen.SetInputValue(ElementKeys.MIN_PRICE_LIST_ITEM, startPrice, ShortWait) != null
                     && _screen.SetInputValue(ElementKeys.MAX_PRICE_LIST_ITEM, listPrice, ShortWait) != null
                     && _screen.Click(ElementKeys.LIST_ITEM, StandardWait);

        Thread.Sleep(2000);

        return listed;
    }

    #endregion

    #region Transfer Targets And Sold Items

    private void ClearNotWonItemsFromTransferTargets()
    {
        _screen.Click(ElementKeys.CLEAR_NOT_WON_TRANSFER_TARGETS, ShortWait);
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
        EnsureLoggedIn();
        RequireClick(ElementKeys.LEFT_HAND_PANE_TRANSFERS);
        RequireTitle("Transfers");

        var totalText = _screen.ReadText(ElementKeys.TRANSFER_TARGETS_TOTAL, ShortWait);

        if (totalText.Length > 0) _total = Utility.Utility.CommaSeperatedNumberToUInt(totalText);
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
