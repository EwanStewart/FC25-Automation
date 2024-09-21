using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;

namespace Automation.Setup
{
    public class Browser
    {
        /// <summary>
        /// Chrome driver object to fire browser commands.
        /// </summary>
        public ChromeDriver Chrome;

        /// <summary>
        /// Constructor for BrowserSetup, create a new browser instance with configuration.
        /// </summary>
        public Browser ()
        {
            Chrome = InitialiseChrome();
        }

        public static string GetChromeConfigurationPath()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string pathToChromeConfiguration = Path.Combine(baseDirectory, "Configuration", "ChromeConfiguration.json");

            if (!File.Exists(pathToChromeConfiguration))
            {
                throw new FileNotFoundException($"The Chrome configuration file was not found at {pathToChromeConfiguration}");
            }

            return pathToChromeConfiguration;
        }

        private static (string, string, string) FetchConfigurationFromJson ()
        {
            string pathToChromeConfiguration = GetChromeConfigurationPath ();
            string configuration = Utility.Utility.ReadJson (pathToChromeConfiguration);

            return (Utility.Utility.GetJsonValue(configuration, "UserDataDir"),
                    Utility.Utility.GetJsonValue(configuration, "UserAgent"),
                    Utility.Utility.GetJsonValue(configuration, "Profile"));
        }

        private static ChromeDriver InitialiseChrome ()
        {
            string profileToUse;
            string userDataDir;
            string userAgent;


            (userDataDir, userAgent, profileToUse) = FetchConfigurationFromJson();

            ChromeOptions options = new ();
            options.AddArgument($"user-agent={userAgent}");
            options.AddArgument($@"--user-data-dir={userDataDir}");
            options.AddArgument($"--profile-directory={profileToUse}");
            options.AddExcludedArgument("enable-automation");
            options.AddAdditionalOption("useAutomationExtension", false);

            ChromeDriver chromeBrowser = new (options);
            chromeBrowser.ExecuteScript("Object.defineProperty(navigator, 'webdriver', {get: () => undefined})");

            return chromeBrowser;
        }

        /// <summary>
        /// Click the desired CSS selector.
        /// </summary>
        /// <param name="chromeDriver"> Chrome instance.</param>
        /// <param name="timeToWait"> Time to wait until timeout. </param>
        /// <param name="elementToClick"> CSS selector to click and alias for logging. </param>
        public static void WaitAndClickElementByCssSelector(ChromeDriver chromeDriver, TimeSpan timeToWait, (string cssSelector, string alias) elementToClick)
        {
            try
            {
                WebDriverWait wait = new (chromeDriver, timeToWait);
                IWebElement element = wait.Until(ExpectedConditions.ElementToBeClickable(By.CssSelector(elementToClick.cssSelector)));
                element.Click();
            }
            catch (WebDriverTimeoutException)
            {
                Console.WriteLine($"Element '{elementToClick.alias}' was not found.");
            }
        }

        /// <summary>
        /// Click the desired XPath.
        /// </summary>
        /// <param name="chromeDriver"> Chrome instance.</param>
        /// <param name="timeToWait"> Time to wait until timeout. </param>
        /// <param name="elementToClick"> XPath to click and alias for logging. </param>
        public static void WaitAndClickElementByXPath(ChromeDriver chromeDriver, TimeSpan timeToWait, (string xPath, string alias) elementToClick)
        {
            try
            {
                WebDriverWait wait = new(chromeDriver, timeToWait);
                IWebElement element = wait.Until(ExpectedConditions.ElementToBeClickable(By.XPath(elementToClick.xPath)));
                Thread.Sleep (500);
                element.Click();
            }
            catch (WebDriverTimeoutException)
            {
                Console.WriteLine($"Element '{elementToClick.alias}' was not found.");
            }
        }

        public static IList<IWebElement>? FindAllElementsWithXPath (ChromeDriver chromeDriver, (string xPath, string alias) element)
        {
            IList<IWebElement>? items = null;

            try
            {
                items = chromeDriver.FindElements(By.XPath(element.xPath));
            }
            catch (WebDriverTimeoutException)
            {
                Console.WriteLine($"Element '{element.alias}' was not found.");
            }

            return items;
        }

        /// <summary>
        /// Return the value of the element's text.
        /// </summary>
        /// <param name="chromeDriver"> Chrome instance.</param>
        /// <param name="timeToWait"> Time to wait until timeout. </param>
        /// <param name="elementToClick"> CSS selector to click and alias for logging. </param>
        public static string WaitAndGetElementTextByCssSelector(ChromeDriver chromeDriver, TimeSpan timeToWait, (string cssSelector, string alias) elementToClick)
        {
            string returnValue = string.Empty;

            try
            {
                WebDriverWait wait = new(chromeDriver, timeToWait);
                IWebElement element = wait.Until(ExpectedConditions.ElementToBeClickable(By.CssSelector(elementToClick.cssSelector)));
                returnValue = element.Text;
            }
            catch (WebDriverTimeoutException)
            {
                Console.WriteLine($"Element '{elementToClick.alias}' was not found.");
            }

            return returnValue;
        }

        public static string WaitAndGetElementTextByXPath(ChromeDriver chromeDriver, TimeSpan timeToWait, (string xPath, string alias) elementToClick)
        {
            string returnValue = string.Empty;

            try
            {
                WebDriverWait wait = new(chromeDriver, timeToWait);
                IWebElement element = wait.Until(ExpectedConditions.ElementToBeClickable(By.XPath(elementToClick.xPath)));
                returnValue = element.Text;
            }
            catch (WebDriverTimeoutException)
            {
                Console.WriteLine($"Element '{elementToClick.alias}' was not found.");
            }

            return returnValue;
        }
    }
}
