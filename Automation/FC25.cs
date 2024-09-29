using Automation.Setup;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using System.Data;
using static Automation.Definitions.FC25Definitions;
using System.Text.Json;

namespace Automation
{
    public class FC25
    {
        const string FC_URL = @"https://www.ea.com/fifa/ultimate-team/web-app/";

        private readonly ChromeDriver driver;
        private static readonly TimeSpan STANDARD_WAIT = TimeSpan.FromSeconds(20);
        private readonly WebDriverWait wait;
        private byte counter;
        private readonly string user;
        private byte transferTargetTotal;
        public uint total;
        #region Constructor
        public FC25 (string configuration)
        {
            try
            {
                Browser browser = new (configuration);
                user = configuration;
                driver = browser.Chrome;
                wait = new(driver, STANDARD_WAIT);

                LoginToFC25();
                GetCoinTotal();
                ListAndBidRoutine();
            }
            catch (Exception)
            {
            }
        }
        #endregion

        #region Master Routines
        private void LoginToFC25()
        {
            driver.Navigate().GoToUrl(FC_URL);

            Browser.WaitAndClickElementByCssSelector(
                                                    driver,
                                                    STANDARD_WAIT,
                                                    Elements[ElementKeys.INITIAL_LOGIN]
                                                    );

            Browser.WaitAndClickElementByXPath(
                                        driver,
                                        STANDARD_WAIT,
                                        Elements[ElementKeys.SECOND_LOGIN]
                                        );

            Browser.WaitAndClickElementByXPath(
                            driver,
                            STANDARD_WAIT,
                            Elements[ElementKeys.CONTINUE]
                            );
        }

        private void ListAndBidRoutine()
        {
            Utility.Utility.RetryAction(ClearItemsFromTransferTargets);
            Utility.Utility.RetryAction(ClearSoldItemsFromTransferList);
            Utility.Utility.RetryAction(ListItemsFromTransferList);

            byte timesToRepeat = 3;

            for (byte i = 1; i < timesToRepeat; i++)
            {
                if (total == 49)
                {
                    break;
                }

                BidOnSilverClubItems(false, i);

                if (total == 49)
                {
                    break;
                }

                BidOnSilverClubItems(true, i);
            }

            BidOnPlayerItems(1);
        }
        #endregion

        #region Sub-Routines
        private void ClearItemsFromTransferTargets ()
        {
            GoToTransfers ();
            GoToTransferTargets ();
            ClearNotWonItemsFromTransferTargets();
            SendWonItemsToTransferListFromTransferTargets();
        }
        #endregion

        private void BidOnItems(byte pageToStart, string itemType, ElementKeys itemMarketElement)
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string directoryPath = $@"{baseDirectory}/Configuration/Filters/Active{itemType}{user}";

            if (!Directory.Exists(directoryPath))
            {
                Console.WriteLine($"Directory {directoryPath} does not exist.");
                return;
            }

            string[] filterFiles = Directory.GetFiles(directoryPath, "*.json");

            if (filterFiles.Length > 0)
            {
                foreach (string fi in filterFiles)
                {
                    string json = Utility.Utility.ReadJson(fi);

                    Filter filterData = JsonSerializer.Deserialize<Filter>(json);

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

                    IList<IWebElement> inputs = driver.FindElements(By.XPath(Elements[ElementKeys.FIND_ALL_PRICE_INPUTS].Item1));

                    UpdateInputElementText(filterData.MaxBidPrice, inputs[1]);
                    UpdateInputElementText(filterData.MinBuyPrice, inputs[2]);
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
        }

        private void BidOnPlayerItems(byte pageToStart)
        {
            BidOnItems(pageToStart, "Player", ElementKeys.PLAYER_ITEMS_TRANSFER_MARKET);
        }

        private void BidOnManagerItems(byte pageToStart)
        {
            BidOnItems(pageToStart, "Manager", ElementKeys.MANAGER_ITEMS_TRANSFER_MARKET);
        }


        private void CustomXPathClick (string xPath)
        {
            try
            {
                WebDriverWait wait = new(driver, STANDARD_WAIT);
                IWebElement element = wait.Until(ExpectedConditions.ElementToBeClickable(By.XPath(xPath)));
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
            GoToTransfers ();
            GoToTransferMarket ();
            SendXPathClickCommandStandardWait(ElementKeys.RESET);
            SendXPathClickCommandStandardWait(ElementKeys.CLUB_ITEMS_TRANSFER_MARKET);
            SendXPathClickCommandStandardWait(ElementKeys.QUALITY_DROPDOWN);
            SendXPathClickCommandStandardWait(ElementKeys.QUALITY_DROPDOWN_SILVER);

            SendXPathClickCommandStandardWait(ElementKeys.CLUB_ITEMS_TYPE_DROPDOWN);

            if (badges)
            {
                SendXPathClickCommandStandardWait(ElementKeys.CLUB_ITEMS_TYPE_DROPDOWN_BADGES);
            }
            else
            {
                SendXPathClickCommandStandardWait(ElementKeys.CLUB_ITEMS_TYPE_DROPDOWN_KITS);
            }

            IList<IWebElement> inputs = driver.FindElements(By.XPath(Elements[ElementKeys.FIND_ALL_PRICE_INPUTS].Item1));

            UpdateInputElementText(500, inputs[1]);
            UpdateInputElementText(1000, inputs[2]);
            SendXPathClickCommandStandardWait(ElementKeys.SEARCH);

            Thread.Sleep(1000);


            for (byte i = 0; i < pageToStart; i++)
            {
                SendXPathClickCommandStandardWait(ElementKeys.NEXT);
                Thread.Sleep(1000);
            }

            CompareAndBid(MIN_BUY_NOW_FOR_BID, 500, false);
        }

        private void CompareAndBid(uint bidThreshold, uint maxBid, bool isFixedBid)
        {
            Dictionary<string, uint> bidItems = new();
            IList<IWebElement> items = driver.FindElements(By.XPath(Elements[ElementKeys.AUCTION_ITEMS].Item1));

            foreach (IWebElement item in items)
            {
                if (total == 49)
                {
                    return;
                }

                Thread.Sleep(500);
                uint lowestPrice = 0;
                uint bidPrice = 0;

                try
                {
                    item.Click();
                }
                catch (StaleElementReferenceException)
                {
                    break;
                }

                string info = GetItemInfo(item);

                if (Database.HasBeenSeenTodayAndLessThanBidThreshold(info, bidThreshold))
                {
                    continue;
                }

                Thread.Sleep(500);

                SendXPathClickCommandStandardWait(ElementKeys.COMPARE_PRICE);

                Thread.Sleep(2000);

                if (!bidItems.TryGetValue(info, out bidPrice))
                {
                    IWebElement comparePriceList = wait.Until(ExpectedConditions.ElementIsVisible(By.XPath("/html/body/main/section/section/div[2]/div/div/section/div[2]/section/div[2]")));

                    if (comparePriceList != null)
                    {
    
                        lowestPrice = (uint)FindLowestPrice(comparePriceList);

                        bidPrice = (uint)Math.Min((lowestPrice * 0.1), maxBid);

                        bidItems.Add(info, bidPrice);

                        Database.AddToSeenTable(lowestPrice, info);

                        SendXPathClickCommandStandardWait(ElementKeys.COMPARE_PRICE_BACK_BUTTON);
                    }
                }

                if (lowestPrice >= bidThreshold)
                {
                    Thread.Sleep(1000);
                    IWebElement bidInput = driver.FindElement(By.XPath(Elements[ElementKeys.FIND_ALL_PRICE_INPUTS].Item1));

                    if (Utility.Utility.CommaSeperatedNumberToUInt(bidInput.GetAttribute("value")) > maxBid)
                    {
                        continue;
                    }

                    UpdateInputElementText(bidPrice, bidInput);

                    SendXPathClickCommandStandardWait(ElementKeys.MAKE_BID);
                    total += 1;
                }

                Thread.Sleep(1000);
            }
        }

        private void Bid(uint maxBid)
        {
            Dictionary<string, uint> bidItems = new();
            IList<IWebElement> items = driver.FindElements(By.XPath(Elements[ElementKeys.AUCTION_ITEMS].Item1));

            foreach (IWebElement item in items)
            {
                if (total == 49)
                {
                    return;
                }

                Thread.Sleep(500);

                try
                {
                    item.Click();
                }
                catch (StaleElementReferenceException)
                {
                    break;
                }

                IWebElement bidInput = driver.FindElement(By.XPath(Elements[ElementKeys.FIND_ALL_PRICE_INPUTS].Item1));

                if (Utility.Utility.CommaSeperatedNumberToUInt(bidInput.GetAttribute("value")) > maxBid)
                {
                    continue;
                }

                UpdateInputElementText(maxBid, bidInput);

                SendXPathClickCommandStandardWait(ElementKeys.MAKE_BID);
                total += 1;


                Thread.Sleep(1000);
            }
        }

        public void UpdateInputElementText(uint listPrice, string xPath)
        {
            WebDriverWait wait = new (driver, TimeSpan.FromSeconds(5));
            IWebElement inputElement = wait.Until(ExpectedConditions.ElementToBeClickable(By.XPath(xPath)));
            inputElement.Click();
            inputElement.SendKeys(Keys.Control + "a");
            inputElement.SendKeys(Keys.Backspace);
            inputElement.SendKeys((listPrice).ToString());
        }

        public void UpdateInputElementText(uint listPrice, IWebElement inputElement)
        {
            inputElement.Click();
            inputElement.SendKeys(Keys.Control + "a");
            inputElement.SendKeys(Keys.Backspace);
            inputElement.SendKeys((listPrice).ToString());
        }

        private string GetItemInfo (IWebElement item)
        {
            string soldName = item.FindElement(By.CssSelector("div.name")).Text;
            string soldType = string.Empty;

            try
            {
                IWebElement typeParent = driver.FindElement(By.CssSelector("div.tns-item.tns-slide-active"));
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

            WebDriverWait wait = new(driver, TimeSpan.FromSeconds(10));

            int clickCounter = 0; // Counter for item clicks

            while (true)
            {
                Thread.Sleep(1000);

                IWebElement item;
                uint listPrice = 0;

                try
                {
                    item = driver.FindElement(By.XPath(Elements[ElementKeys.LISTABLE_ITEMS].Item1));
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

                string info = GetItemInfo(item);

                if (!listItems.TryGetValue(info, out listPrice))
                {
                    SendXPathClickCommandStandardWait(ElementKeys.COMPARE_PRICE);
                    Thread.Sleep(1000);

                    IWebElement comparePriceList = wait.Until(ExpectedConditions.ElementIsVisible(By.XPath("/html/body/main/section/section/div[2]/div/div/section/div[2]/section/div[2]")));
                    if (comparePriceList != null)
                    {
                        uint lowestPrice = (uint)FindLowestPrice(comparePriceList);
                        listPrice = (uint)(lowestPrice - (lowestPrice * 0.1));

                        Database.AddToSeenTable(lowestPrice, info);
                        listItems.Add(info, listPrice);
                        item.Click();
                    }
                }

                SendXPathClickCommandStandardWait(ElementKeys.LIST_ITEM_PRE_PRICE);
                UpdateInputElementText(listPrice - 100, Elements[ElementKeys.MIN_PRICE_LIST_ITEM].Item1);
                UpdateInputElementText(listPrice, Elements[ElementKeys.MAX_PRICE_LIST_ITEM].Item1);

                SendXPathClickCommandStandardWait(ElementKeys.LIST_ITEM);

                Thread.Sleep(2000);
            }
        }

        private int FindLowestPrice (IWebElement list)
        {
            IList<IWebElement> buyNowSpans = list.FindElements(By.XPath(".//span[text()='Buy Now:']"));
            List<int> buyNowPrices = [];
            int lowestPrice = 5000;

            foreach (IWebElement span in buyNowSpans)
            {
                IWebElement priceElement = span.FindElement(By.XPath("./following-sibling::span"));
                string priceText = priceElement.Text;
                buyNowPrices.Add((int)Utility.Utility.CommaSeperatedNumberToUInt(priceText));
            }

            if (buyNowPrices.Count != 0)
            {
                double mean = buyNowPrices.Average();
                double standardDeviation = Math.Sqrt(buyNowPrices.Average(p => Math.Pow(p - mean, 2)));

                double lowerBound = mean - 2 * standardDeviation;
                double upperBound = mean + 2 * standardDeviation;

                List<int> filteredPrices = buyNowPrices
                    .Where(price => price >= lowerBound && price <= upperBound)
                    .ToList();

                if (filteredPrices.Count != 0)
                {
                    lowestPrice = filteredPrices.Min();
                }
            }

            return lowestPrice;
        }

        private void ClearSoldItemsFromTransferList()
        {
            GoToTransfers ();
            GoToTransferList ();
            ClearWonItemsFromTransferList ();
        }

        #region Common Events
        private void SendXPathClickCommandStandardWait(ElementKeys key)
        {
            Browser.WaitAndClickElementByXPath
             (
                 driver,
                 STANDARD_WAIT,
                 Elements[key]
             );
        }

        private IList<IWebElement>? FindAllElementsWithXPath (ElementKeys key)
        {
            IList<IWebElement>? elements = Browser.FindAllElementsWithXPath
            (
               driver,
               Elements[key]
            );

            return elements;
        }

        private void GoToTransfers()
        {
            SendXPathClickCommandStandardWait(ElementKeys.LEFT_HAND_PANE_TRANSFERS);
            total = Utility.Utility.CommaSeperatedNumberToUInt(driver.FindElements(By.XPath(Elements[ElementKeys.TRANSFER_TARGETS_TOTAL].Item1))[1].Text);
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
            IList<IWebElement>? items = FindAllElementsWithXPath(ElementKeys.WON_TARGET);
            WebDriverWait wait = new (driver, TimeSpan.FromSeconds(10));

            if (items != null)
            {
                foreach (IWebElement item in items)
                {
                    wait.Until(ExpectedConditions.ElementToBeClickable(item));

                    item.Click();

                    Thread.Sleep(1000);

                    SendXPathClickCommandStandardWait(ElementKeys.SEND_TO_TRANSFER_LIST);

                    Thread.Sleep(1000);
                }
            }
        }



        private void ClearWonItemsFromTransferList()
        {
            IList<IWebElement>? items = FindAllElementsWithXPath(ElementKeys.WON_TARGET);
            WebDriverWait wait = new(driver, TimeSpan.FromSeconds(10));

            if (items != null)
            {
                foreach (IWebElement item in items)
                {
                    wait.Until(ExpectedConditions.ElementToBeClickable(item));

                    try
                    {
                        item.Click();
                    }
                    catch (StaleElementReferenceException)
                    {
                        ClearWonItemsFromTransferList ();
                        break;
                    }

                    IList<IWebElement> priceElements = item.FindElements(By.CssSelector("span.currency-coins.value"));
                    string soldName = item.FindElement(By.CssSelector("div.name")).Text;
                    string soldType = string.Empty;

                    string soldPriceString = priceElements.Count > 0 ? priceElements[^1].Text : "0";
                    uint soldPrice = Utility.Utility.CommaSeperatedNumberToUInt(soldPriceString);

                    try
                    {
                        IWebElement typeParent = driver.FindElement(By.CssSelector("div.tns-item.tns-slide-active"));
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
            string coinTotalAsString = Browser.WaitAndGetElementTextByCssSelector(driver,
                                                                    STANDARD_WAIT,
                                                                    ("div.view-navbar-currency-coins", "Coin Total Text")
                                                                    );

            uint coinTotal = Utility.Utility.CommaSeperatedNumberToUInt(coinTotalAsString);

            Database.AddToCoinTable(coinTotal);
        }
        #endregion
    }
}
