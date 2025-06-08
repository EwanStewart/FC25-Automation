using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using System.Diagnostics;

namespace Automation.Setup;

public class Browser
{
    /// <summary>
    /// Chrome driver object to fire browser commands.
    /// </summary>
    public ChromeDriver Chrome;

    private readonly string _configurationToUse;

    /// <summary>
    /// Constructor for BrowserSetup, create a new browser instance with configuration.
    /// </summary>
    public Browser(string profileArg)
    {
        _configurationToUse = profileArg;
        Chrome = CreateDefaultChromeDriver();
    }

    public string GetChromeConfigurationPath()
    {
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var pathToChromeConfiguration =
            Path.Combine(baseDirectory, "Configuration", $"ChromeConfiguration{_configurationToUse}.json");

        if (!File.Exists(pathToChromeConfiguration))
            throw new FileNotFoundException(
                $"The Chrome configuration file was not found at {pathToChromeConfiguration}");

        return pathToChromeConfiguration;
    }

    private (string, string, string) FetchConfigurationFromJson()
    {
        var pathToChromeConfiguration = GetChromeConfigurationPath();
        var configuration = Utility.Utility.ReadJson(pathToChromeConfiguration);

        return (Utility.Utility.GetJsonValue(configuration, "UserDataDir"),
            Utility.Utility.GetJsonValue(configuration, "UserAgent"),
            Utility.Utility.GetJsonValue(configuration, "Profile"));
    }

    private void KillChromeProcesses()
    {
        foreach (var process in Process.GetProcessesByName("chrome"))
            try
            {
                process.Kill();
                process.WaitForExit();
            }
            catch
            {
            }
    }

    /// <summary>
    /// Create a new ChromeDriver instance with default profile and basic options.
    /// </summary>
    private ChromeDriver CreateDefaultChromeDriver()
    {
        KillChromeProcesses();
        var options = new ChromeOptions();

        // Default Chrome options for stability
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-dev-shm-usage");
        options.AddArgument("--disable-extensions");
        options.AddArgument("--disable-gpu");

        options.AddArgument("--user-data-dir=C:/SeleniumChromeProfile");
        options.AddArgument("--remote-debugging-port=9222");

        return new ChromeDriver(options);
    }

    /// <summary>
    /// Recursively copy a directory and its contents.
    /// </summary>
    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            var destFile = Path.Combine(destinationDir, fileName);
            File.Copy(file, destFile, true);
        }

        foreach (var directory in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(directory);
            var destDir = Path.Combine(destinationDir, dirName);
            CopyDirectory(directory, destDir);
        }
    }


    /// <summary>
    /// Click the desired CSS selector.
    /// </summary>
    /// <param name="chromeDriver"> Chrome instance.</param>
    /// <param name="timeToWait"> Time to wait until timeout. </param>
    /// <param name="elementToClick"> CSS selector to click and alias for logging. </param>
    public static void WaitAndClickElementByCssSelector(ChromeDriver chromeDriver, TimeSpan timeToWait,
        (string cssSelector, string alias) elementToClick)
    {
        try
        {
            WebDriverWait wait = new(chromeDriver, timeToWait);
            var element =
                wait.Until(ExpectedConditions.ElementToBeClickable(By.CssSelector(elementToClick.cssSelector)));
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
    public static void WaitAndClickElementByXPath(ChromeDriver chromeDriver, TimeSpan timeToWait,
        (string xPath, string alias) elementToClick)
    {
        try
        {
            WebDriverWait wait = new(chromeDriver, timeToWait);
            var element = wait.Until(ExpectedConditions.ElementToBeClickable(By.XPath(elementToClick.xPath)));
            Thread.Sleep(500);
            element.Click();
        }
        catch (WebDriverTimeoutException)
        {
            Console.WriteLine($"Element '{elementToClick.alias}' was not found.");
        }
    }

    public static IList<IWebElement>? FindAllElementsWithXPath(ChromeDriver chromeDriver,
        (string xPath, string alias) element)
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
    public static string WaitAndGetElementTextByCssSelector(ChromeDriver chromeDriver, TimeSpan timeToWait,
        (string cssSelector, string alias) elementToClick)
    {
        var returnValue = string.Empty;

        try
        {
            WebDriverWait wait = new(chromeDriver, timeToWait);
            var element =
                wait.Until(ExpectedConditions.ElementToBeClickable(By.CssSelector(elementToClick.cssSelector)));
            returnValue = element.Text;
        }
        catch (WebDriverTimeoutException)
        {
            Console.WriteLine($"Element '{elementToClick.alias}' was not found.");
        }

        return returnValue;
    }

    public static string WaitAndGetElementTextByXPath(ChromeDriver chromeDriver, TimeSpan timeToWait,
        (string xPath, string alias) elementToClick)
    {
        var returnValue = string.Empty;

        try
        {
            WebDriverWait wait = new(chromeDriver, timeToWait);
            var element = wait.Until(ExpectedConditions.ElementToBeClickable(By.XPath(elementToClick.xPath)));
            returnValue = element.Text;
        }
        catch (WebDriverTimeoutException)
        {
            Console.WriteLine($"Element '{elementToClick.alias}' was not found.");
        }

        return returnValue;
    }
}