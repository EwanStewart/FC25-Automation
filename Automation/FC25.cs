using Automation.Setup;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using static Automation.Definitions.Fc25Definitions;
using System.Text.Json;

namespace Automation;

public class Fc25
{
    private const string FcUrl = @"https://www.ea.com/fifa/ultimate-team/web-app/";

    private readonly ChromeDriver _driver;
    private static readonly TimeSpan StandardWait = TimeSpan.FromSeconds(10);
    private readonly WebDriverWait _wait;
    private readonly string _user;
    private uint _total;

    #region Constructor

    public Fc25(string configuration)
    {
        try
        {
            Browser browser = new(configuration);
            _user = configuration;
            _driver = browser.Chrome;
            _wait = new WebDriverWait(_driver, StandardWait);

            LoginToFc25();
            GetCoinTotal();
            ListAndBidRoutine();
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception.Message);
        }
    }

    #endregion

    #region Master Routines

    private void LoginToFc25()
    {
        _driver.Navigate().GoToUrl(FcUrl);

        Browser.WaitAndClickElementByCssSelector(
            _driver,
            StandardWait,
            Elements[ElementKeys.INITIAL_LOGIN]
        );

        Browser.WaitAndClickElementByXPath(
            _driver,
            StandardWait,
            Elements[ElementKeys.SECOND_LOGIN]
        );

        Browser.WaitAndClickElementByXPath(
            _driver,
            StandardWait,
            Elements[ElementKeys.CONTINUE]
        );
    }

    private void ListAndBidRoutine()
    {
        Utility.Utility.RetryAction(ClearItemsFromTransferTargets);
        Utility.Utility.RetryAction(ClearSoldItemsFromTransferList);
        Utility.Utility.RetryAction(ListItemsFromTransferList);

        const byte timesToRepeat = 3;

        for (byte i = 1; i < timesToRepeat; i++)
        {
            if (_total == 49) break;

            BidOnSilverClubItems(false, i);

            if (_total == 49) break;

            BidOnSilverClubItems(true, i);
        }

        BidOnPlayerItems(1);
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

    #endregion

    private void BidOnItems(byte pageToStart, string itemType, ElementKeys itemMarketElement)
    {
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var directoryPath = $@"{baseDirectory}/Configuration/Filters/Active{itemType}{_user}";

        if (!Directory.Exists(directoryPath))
        {
            Console.WriteLine($"Directory {directoryPath} does not exist.");
            return;
        }

        var filterFiles = Directory.GetFiles(directoryPath, "*.json");

        if (filterFiles.Length > 0)
            foreach (var fi in filterFiles)
            {
                var json = Utility.Utility.ReadJson(fi);

                var filterData = JsonSerializer.Deserialize<Filter>(json);

                GoToTransfers();
                GoToTransferMarket();
                SendXPathClickCommandStandardWait(itemMarketElement);
                SendXPathClickCommandStandardWait(ElementKeys.RESET);

                if (filterData.Quality != null)
                {
                    SendXPathClickCommandStandardWait(ElementKeys.QUALITY_DROPDOWN);
                    CustomXPathClick($"//li[text()='{filterData.Quality}']");
                }

                if (filterData.Nationality != null)
                {
                    SendXPathClickCommandStandardWait(ElementKeys.NATIONALITY_DROPDOWN);
                    CustomXPathClick($"//li[text()='{filterData.Nationality}']");
                }

                if (filterData.Rarity != null)
                {
                    SendXPathClickCommandStandardWait(ElementKeys.RARITY_DROPDOWN);
                    CustomXPathClick($"//li[text()='{filterData.Rarity}']");
                }


                IList<IWebElement> inputs =
                    _driver.FindElements(By.XPath(Elements[ElementKeys.FIND_ALL_PRICE_INPUTS].Item1));

                for (var i = 0; i < inputs.Count; i++)
                    try
                    {
                        var inputId = inputs[i].GetAttribute("id") ?? "no-id";
                        var inputName = inputs[i].GetAttribute("name") ?? "no-name";
                        var inputPlaceholder = inputs[i].GetAttribute("placeholder") ?? "no-placeholder";
                        var inputValue = inputs[i].GetAttribute("value") ?? "empty";
                        var inputClass = inputs[i].GetAttribute("class") ?? "no-class";

                        Console.WriteLine(
                            $"Input[{i}]: ID='{inputId}', Name='{inputName}', Placeholder='{inputPlaceholder}', Value='{inputValue}', Class='{inputClass}'");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Input[{i}]: Error getting attributes - {ex.Message}");
                    }

                UpdateInputElementText(filterData.MaxBidPrice,
                    "/html/body/main/section/section/div[2]/div/div[2]/div/div[1]/div[2]/div[3]/div[2]/input");
                UpdateInputElementText(filterData.MinBuyPrice,
                    "/html/body/main/section/section/div[2]/div/div[2]/div/div[1]/div[2]/div[5]/div[2]/input");
                SendXPathClickCommandStandardWait(ElementKeys.SEARCH);
                Thread.Sleep(1000);

                for (byte i = 0; i < pageToStart; i++)
                {
                    SendXPathClickCommandStandardWait(ElementKeys.NEXT);
                    Thread.Sleep(1000);
                }

                Bid(filterData.MaxBidPrice);
            }
    }

    private void BidOnPlayerItems(byte pageToStart)
    {
        BidOnItems(pageToStart, "Player", ElementKeys.PLAYER_ITEMS_TRANSFER_MARKET);
    }

    private void BidOnManagerItems(byte pageToStart)
    {
        BidOnItems(pageToStart, "Manager", ElementKeys.MANAGER_ITEMS_TRANSFER_MARKET);
    }


    private void CustomXPathClick(string xPath)
    {
        try
        {
            WebDriverWait wait = new(_driver, StandardWait);
            var element = wait.Until(ExpectedConditions.ElementToBeClickable(By.XPath(xPath)));
            Thread.Sleep(500);
            element.Click();
        }
        catch (WebDriverTimeoutException)
        {
            throw new Exception($"Invalid XPath {xPath}");
        }
    }

    private void BidOnSilverClubItems(bool badges, byte pageToStart)
    {
        GoToTransfers();
        GoToTransferMarket();
        SendXPathClickCommandStandardWait(ElementKeys.RESET);
        SendXPathClickCommandStandardWait(ElementKeys.CLUB_ITEMS_TRANSFER_MARKET);
        SendXPathClickCommandStandardWait(ElementKeys.QUALITY_DROPDOWN);
        SendXPathClickCommandStandardWait(ElementKeys.QUALITY_DROPDOWN_SILVER);

        SendXPathClickCommandStandardWait(ElementKeys.CLUB_ITEMS_TYPE_DROPDOWN);

        SendXPathClickCommandStandardWait(badges
            ? ElementKeys.CLUB_ITEMS_TYPE_DROPDOWN_BADGES
            : ElementKeys.CLUB_ITEMS_TYPE_DROPDOWN_KITS);

        IList<IWebElement> inputs = _driver.FindElements(By.XPath(Elements[ElementKeys.FIND_ALL_PRICE_INPUTS].Item1));

        UpdateInputElementText(500, inputs[3]);
        UpdateInputElementText(1000, inputs[4]);
        SendXPathClickCommandStandardWait(ElementKeys.SEARCH);

        Thread.Sleep(1000);


        for (byte i = 0; i < pageToStart; i++)
        {
            SendXPathClickCommandStandardWait(ElementKeys.NEXT);
            Thread.Sleep(1000);
        }

        CompareAndBid(MinBuyNowForBid, 500);
    }

    private void CompareAndBid(uint bidThreshold, uint maxBid)
    {
        Dictionary<string, uint> bidItems = new();
        IList<IWebElement> items = _driver.FindElements(By.XPath(Elements[ElementKeys.AUCTION_ITEMS].Item1));

        foreach (var item in items)
        {
            if (_total == 49) return;

            Thread.Sleep(500);
            uint lowestPrice = 0;

            try
            {
                item.Click();
            }
            catch (StaleElementReferenceException)
            {
                break;
            }

            var info = GetItemInfo(item);

            if (Database.HasBeenSeenTodayAndLessThanBidThreshold(info, bidThreshold)) continue;

            Thread.Sleep(500);

            SendXPathClickCommandStandardWait(ElementKeys.COMPARE_PRICE);

            Thread.Sleep(2000);

            if (!bidItems.TryGetValue(info, out var bidPrice))
            {
                var comparePriceList = _wait.Until(ExpectedConditions.ElementIsVisible(
                    By.XPath("/html/body/main/section/section/div[2]/div/div/section/div[2]/section/div[2]")));

                if (comparePriceList != null)
                {
                    lowestPrice = (uint)FindLowestPrice(comparePriceList);

                    bidPrice = (uint)Math.Min(lowestPrice * 0.1, maxBid);

                    bidItems.Add(info, bidPrice);

                    Database.AddToSeenTable(lowestPrice, info);

                    SendXPathClickCommandStandardWait(ElementKeys.COMPARE_PRICE_BACK_BUTTON);
                }
            }

            if (lowestPrice >= bidThreshold)
            {
                Thread.Sleep(1000);
                var bidInput = _driver.FindElement(By.XPath(Elements[ElementKeys.FIND_ALL_PRICE_INPUTS].Item1));

                if (Utility.Utility.CommaSeperatedNumberToUInt(bidInput.GetAttribute("value")) > maxBid) continue;

                UpdateInputElementText(bidPrice, bidInput);

                SendXPathClickCommandStandardWait(ElementKeys.MAKE_BID);
                _total += 1;
            }

            Thread.Sleep(1000);
        }
    }

    private void Bid(uint maxBid)
    {
        Dictionary<string, uint> bidItems = new();
        IList<IWebElement> items = _driver.FindElements(By.XPath(Elements[ElementKeys.AUCTION_ITEMS].Item1));

        foreach (var item in items)
        {
            if (_total == 49) return;

            Thread.Sleep(500);

            try
            {
                item.Click();
            }
            catch (StaleElementReferenceException)
            {
                break;
            }

            var bidInput = _driver.FindElement(By.XPath(Elements[ElementKeys.FIND_ALL_PRICE_INPUTS].Item1));

            if (Utility.Utility.CommaSeperatedNumberToUInt(bidInput.GetAttribute("value")) > maxBid) continue;

            UpdateInputElementText(maxBid, bidInput);

            SendXPathClickCommandStandardWait(ElementKeys.MAKE_BID);
            _total += 1;


            Thread.Sleep(1000);
        }
    }

    private void UpdateInputElementText(uint listPrice, string xPath)
    {
        WebDriverWait wait = new(_driver, TimeSpan.FromSeconds(5));
        var inputElement = wait.Until(ExpectedConditions.ElementToBeClickable(By.XPath(xPath)));
        inputElement.Click();
        inputElement.SendKeys(Keys.Control + "a");
        inputElement.SendKeys(Keys.Backspace);
        inputElement.SendKeys(listPrice.ToString());
    }

    private static void UpdateInputElementText(uint listPrice, IWebElement inputElement)
    {
        inputElement.Click();
        inputElement.SendKeys(Keys.Control + "a");
        inputElement.SendKeys(Keys.Backspace);
        inputElement.SendKeys(listPrice.ToString());
    }

    private string GetItemInfo(IWebElement item)
    {
        var soldName = item.FindElement(By.CssSelector("div.name")).Text;
        var soldType = string.Empty;

        try
        {
            var typeParent = _driver.FindElement(By.CssSelector("div.tns-item.tns-slide-active"));
            soldType = typeParent.FindElement(By.CssSelector("div.clubView")).Text;
        }
        catch (NoSuchElementException)
        {
        }

        return $"{soldName} {soldType}";
    }

    private void ListItemsFromTransferList()
    {
        Dictionary<string, uint> listItems = new();

        GoToTransfers();
        GoToTransferList();

        WebDriverWait wait = new(_driver, TimeSpan.FromSeconds(10));

        var clickCounter = 0; // Counter for item clicks

        while (true)
        {
            Thread.Sleep(1000);

            IWebElement item;

            try
            {
                item = _driver.FindElement(By.XPath(Elements[ElementKeys.LISTABLE_ITEMS].Item1));
            }
            catch (NoSuchElementException)
            {
                break;
            }

            item.Click();
            clickCounter++; // Increment the click counter

            if (clickCounter == 10)
            {
                ListItemsFromTransferList();
                clickCounter = 0;
            }

            Thread.Sleep(500);

            var info = GetItemInfo(item);

            if (!listItems.TryGetValue(info, out var listPrice))
            {
                SendXPathClickCommandStandardWait(ElementKeys.COMPARE_PRICE);
                Thread.Sleep(1000);

                var comparePriceList = wait.Until(ExpectedConditions.ElementIsVisible(
                    By.XPath("/html/body/main/section/section/div[2]/div/div/section/div[2]/section/div[2]")));
                if (comparePriceList != null)
                {
                    var lowestPrice = (uint)FindLowestPrice(comparePriceList);
                    listPrice = (uint)(lowestPrice - lowestPrice * 0.1);

                    Database.AddToSeenTable(lowestPrice, info);
                    listItems.Add(info, listPrice);
                    SendXPathClickCommandStandardWait(ElementKeys.COMPARE_PRICE_BACK_BUTTON);
                }
            }

            SendXPathClickCommandStandardWait(ElementKeys.LIST_ITEM_PRE_PRICE);
            UpdateInputElementText(listPrice - 100, Elements[ElementKeys.MIN_PRICE_LIST_ITEM].Item1);
            UpdateInputElementText(listPrice, Elements[ElementKeys.MAX_PRICE_LIST_ITEM].Item1);

            SendXPathClickCommandStandardWait(ElementKeys.LIST_ITEM);

            Thread.Sleep(2000);
        }
    }

    private static int FindLowestPrice(IWebElement list)
    {
        IList<IWebElement> buyNowSpans = list.FindElements(By.XPath(".//span[text()='Buy Now:']"));
        List<int> buyNowPrices = [];
        var lowestPrice = 5000;

        foreach (var span in buyNowSpans)
        {
            var priceElement = span.FindElement(By.XPath("./following-sibling::span"));
            var priceText = priceElement.Text;
            buyNowPrices.Add((int)Utility.Utility.CommaSeperatedNumberToUInt(priceText));
        }

        if (buyNowPrices.Count != 0)
        {
            var mean = buyNowPrices.Average();
            var standardDeviation = Math.Sqrt(buyNowPrices.Average(p => Math.Pow(p - mean, 2)));

            var lowerBound = mean - 2 * standardDeviation;
            var upperBound = mean + 2 * standardDeviation;

            var filteredPrices = buyNowPrices
                .Where(price => price >= lowerBound && price <= upperBound)
                .ToList();

            if (filteredPrices.Count != 0) lowestPrice = filteredPrices.Min();
        }

        return lowestPrice;
    }

    private void ClearSoldItemsFromTransferList()
    {
        GoToTransfers();
        GoToTransferList();
        ClearWonItemsFromTransferList();
    }

    #region Common Events

    private void SendXPathClickCommandStandardWait(ElementKeys key)
    {
        Browser.WaitAndClickElementByXPath
        (
            _driver,
            StandardWait,
            Elements[key]
        );
    }

    private IList<IWebElement>? FindAllElementsWithXPath(ElementKeys key)
    {
        var elements = Browser.FindAllElementsWithXPath
        (
            _driver,
            Elements[key]
        );

        return elements;
    }

    private void GoToTransfers()
    {
        SendXPathClickCommandStandardWait(ElementKeys.LEFT_HAND_PANE_TRANSFERS);
        _total = Utility.Utility.CommaSeperatedNumberToUInt(
            _driver.FindElements(By.XPath(Elements[ElementKeys.TRANSFER_TARGETS_TOTAL].Item1))[0].Text);
    }

    private void GoToTransferList()
    {
        SendXPathClickCommandStandardWait(ElementKeys.TRANSFER_LIST);
    }

    private void GoToTransferTargets()
    {
        SendXPathClickCommandStandardWait(ElementKeys.TRANSFER_TARGETS);
    }

    private void GoToTransferMarket()
    {
        SendXPathClickCommandStandardWait(ElementKeys.TRANSFER_MARKET);
    }

    private void ClearNotWonItemsFromTransferTargets()
    {
        SendXPathClickCommandStandardWait(ElementKeys.CLEAR_NOT_WON_TRANSFER_TARGETS);
    }

    private void SendWonItemsToTransferListFromTransferTargets()
    {
        var items = FindAllElementsWithXPath(ElementKeys.WON_TARGET);
        WebDriverWait wait = new(_driver, TimeSpan.FromSeconds(10));

        if (items != null)
            foreach (var item in items)
            {
                wait.Until(ExpectedConditions.ElementToBeClickable(item));

                item.Click();

                Thread.Sleep(1000);

                SendXPathClickCommandStandardWait(ElementKeys.SEND_TO_TRANSFER_LIST);

                Thread.Sleep(1000);
            }
    }


    private void ClearWonItemsFromTransferList()
    {
        var items = FindAllElementsWithXPath(ElementKeys.WON_TARGET);
        WebDriverWait wait = new(_driver, TimeSpan.FromSeconds(10));

        if (items != null)
        {
            foreach (var item in items)
            {
                wait.Until(ExpectedConditions.ElementToBeClickable(item));

                try
                {
                    item.Click();
                }
                catch (StaleElementReferenceException)
                {
                    ClearWonItemsFromTransferList();
                    break;
                }

                IList<IWebElement> priceElements = item.FindElements(By.CssSelector("span.currency-coins.value"));
                var soldName = item.FindElement(By.CssSelector("div.name")).Text;
                var soldType = string.Empty;

                var soldPriceString = priceElements.Count > 0 ? priceElements[^1].Text : "0";
                var soldPrice = Utility.Utility.CommaSeperatedNumberToUInt(soldPriceString);

                try
                {
                    var typeParent = _driver.FindElement(By.CssSelector("div.tns-item.tns-slide-active"));
                    soldType = typeParent.FindElement(By.CssSelector("div.clubView")).Text;
                }
                catch (NoSuchElementException)
                {
                }

                Database.AddToSoldTable(soldPrice, $"{soldName} {soldType}");

                Thread.Sleep(500);
            }

            SendXPathClickCommandStandardWait(ElementKeys.CLEAR_SOLD_TRANSFERS);
        }
    }

    private void GetCoinTotal()
    {
        var coinTotalAsString = Browser.WaitAndGetElementTextByXPath(_driver,
            StandardWait,
            ("/html/body/main/section/section/div[1]/div[1]/div[1]", "Coin Total Text")
        );

        var coinTotal = Utility.Utility.CommaSeperatedNumberToUInt(coinTotalAsString);

        Database.AddToCoinTable(coinTotal);
    }

    #endregion
}