using Automation.Setup;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using Org.BouncyCastle.Bcpg;
using SeleniumExtras.WaitHelpers;
using System.Data;
using System.Linq;
using System.Xml.Linq;
using static Automation.Definitions.FC25Definitions;

namespace Automation
{
    public class FC25
    {
        const string FC_URL = @"https://www.ea.com/fifa/ultimate-team/web-app/";

        private readonly ChromeDriver driver;
        private static readonly TimeSpan STANDARD_WAIT = TimeSpan.FromSeconds(10);
        private WebDriverWait wait; 

        #region Constructor
        public FC25 ()
        {
            Browser browser = new();
            driver = browser.Chrome;
            wait = new(driver, TimeSpan.FromSeconds(10));
            try
            {
                LoginToFC25();
                GetCoinTotal();
                ListAndBidRoutine();
            }
            catch (Exception)
            {
                driver.Close();
                throw;
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

        }

        private void ListAndBidRoutine()
        {
            try
            {
                ClearItemsFromTransferTargets();
            }
            catch (NoSuchElementException) {}

            try
            {
                ClearSoldItemsFromTransferList();
            }
            catch (NoSuchElementException) {}


            try
            {
                ListItemsFromTransferList();
            }
            catch (NoSuchElementException) {}


            try
            {
                BidOnSilverClubItems(false);
            }
            catch (NoSuchElementException) {}


            try
            {
                BidOnSilverClubItems(true);
            }
            catch (NoSuchElementException) {}
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

        private void BidOnSilverBadges()
        {
            throw new NotImplementedException();
        }

        private void BidOnSilverClubItems(bool badges)
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

            IList<IWebElement> items = driver.FindElements(By.XPath(Elements[ElementKeys.AUCTION_ITEMS].Item1));

            foreach (IWebElement item in items)
            {
                uint lowestPrice = 0;
                uint bidPrice = 0;

                item.Click();

                string info = GetItemInfo(item);

                if (Database.HasBeenSeenTodayAndLessThanBidThreshold(info))
                {
                    continue;
                }

                Thread.Sleep(500);

                SendXPathClickCommandStandardWait(ElementKeys.COMPARE_PRICE);

                Thread.Sleep(2000);

                IWebElement comparePriceList = wait.Until(ExpectedConditions.ElementIsVisible(By.XPath("/html/body/main/section/section/div[2]/div/div/section/div[2]/section/div[2]")));
                
                if (comparePriceList != null)
                {
                    lowestPrice = (uint)FindLowestPrice(comparePriceList);
                    bidPrice = (uint)Math.Min(lowestPrice - (lowestPrice * 0.1), 500);

                    Database.AddToSeenTable(lowestPrice, info);
                }

                SendXPathClickCommandStandardWait(ElementKeys.COMPARE_PRICE_BACK_BUTTON);

                if (lowestPrice >= MIN_BUY_NOW_FOR_BID)
                {
                    Thread.Sleep(1000);
                    IWebElement bidInput = driver.FindElement(By.XPath(Elements[ElementKeys.FIND_ALL_PRICE_INPUTS].Item1));
                    UpdateInputElementText(bidPrice, bidInput);
                    SendXPathClickCommandStandardWait(ElementKeys.MAKE_BID);
                }

                Thread.Sleep(1000);
            }
        }

        private void ListNewItemsFromTransferList()
        {
            throw new NotImplementedException();
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
            GoToTransfers();
            GoToTransferList();

            WebDriverWait wait = new (driver, TimeSpan.FromSeconds(10));

            while (true)
            {
                IWebElement item;

                try
                {
                    item = driver.FindElement(By.XPath(Elements[ElementKeys.LISTABLE_ITEMS].Item1));
                }
                catch (NoSuchElementException)
                {
                    break;
                }

                item.Click();

                Thread.Sleep(500);

                SendXPathClickCommandStandardWait(ElementKeys.COMPARE_PRICE);
                Thread.Sleep(1000);

                IWebElement comparePriceList = wait.Until(ExpectedConditions.ElementIsVisible(By.XPath("/html/body/main/section/section/div[2]/div/div/section/div[2]/section/div[2]")));
                if (comparePriceList != null)
                {
                    uint lowestPrice = (uint) FindLowestPrice(comparePriceList);
                    uint listPrice = (uint)(lowestPrice - (lowestPrice * 0.1));


                    try
                    {
                        Database.AddToSeenTable(lowestPrice, GetItemInfo(item));
                        item.Click();
                    }
                    catch (StaleElementReferenceException)
                    {
                        ListItemsFromTransferList ();
                        return;
                    }

                    SendXPathClickCommandStandardWait(ElementKeys.LIST_ITEM_PRE_PRICE);
                    UpdateInputElementText(listPrice - 100, Elements[ElementKeys.MIN_PRICE_LIST_ITEM].Item1);
                    UpdateInputElementText(listPrice, Elements[ElementKeys.MAX_PRICE_LIST_ITEM].Item1);

                    SendXPathClickCommandStandardWait(ElementKeys.LIST_ITEM);

                    Thread.Sleep(500);
                }                       
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
                    wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(item));

                    item.Click();

                    Thread.Sleep(500);

                    SendXPathClickCommandStandardWait(ElementKeys.SEND_TO_TRANSFER_LIST);
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
