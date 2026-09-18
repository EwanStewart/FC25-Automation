using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using System.Diagnostics;
using System.Net.Http;

namespace Automation.Setup;

public class Browser
{
    /// <summary>
    /// Chrome driver object to fire browser commands.
    /// </summary>
    public ChromeDriver Chrome;

    private readonly string _configurationToUse;

    private const int DEBUGGING_PORT = 9222;
    public static readonly string DebuggerHttp = $"http://127.0.0.1:{DEBUGGING_PORT}";

    private static readonly string[] AuthenticationFiles =
        { "Cookies", "Login Data", "Preferences", "Secure Preferences", "Web Data" };

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

    private static void KillChromeProcesses(string profileDirectory)
    {
        if (OperatingSystem.IsWindows())
            KillAllChromeProcesses();
        else
            KillChromeProcessesUsingProfile(profileDirectory);
    }

    private static void KillAllChromeProcesses()
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

    private static void KillChromeProcessesUsingProfile(string profileDirectory)
    {
        using var pkill = Process.Start("pkill", $"-f {profileDirectory}");
        pkill?.WaitForExit();
    }

    private static string GetSeleniumProfileDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SeleniumChromeProfile");
    }

    private static string ExpandHome(string path)
    {
        var result = path;

        if (path.StartsWith('~'))
            result = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[1..].TrimStart('/', '\\'));

        return result;
    }

    private static void CopyFileIfExists(string source, string destination)
    {
        if (File.Exists(source)) File.Copy(source, destination, true);
    }

    private void CopyAuthenticationFiles(string profileDirectory)
    {
        var destinationProfile = Path.Combine(profileDirectory, "Default");

        if (!File.Exists(Path.Combine(destinationProfile, "Cookies"))) CopyAuthenticationFilesInto(destinationProfile);
    }

    private void CopyAuthenticationFilesInto(string destinationProfile)
    {
        var (userDataDir, _, profile) = FetchConfigurationFromJson();
        var sourceRoot = ExpandHome(userDataDir);
        var sourceProfile = Path.Combine(sourceRoot, profile);
        var profileDirectory = Path.GetDirectoryName(destinationProfile)!;

        Directory.CreateDirectory(destinationProfile);
        CopyFileIfExists(Path.Combine(sourceRoot, "Local State"), Path.Combine(profileDirectory, "Local State"));

        foreach (var fileName in AuthenticationFiles)
            CopyFileIfExists(Path.Combine(sourceProfile, fileName), Path.Combine(destinationProfile, fileName));
    }

    /// <summary>
    /// Create a new ChromeDriver instance with default profile and basic options.
    /// </summary>
    private ChromeDriver CreateDefaultChromeDriver()
    {
        var profileDirectory = GetSeleniumProfileDirectory();
        KillChromeProcesses(profileDirectory);
        CopyAuthenticationFiles(profileDirectory);
        LaunchChrome(profileDirectory);
        WaitForDebuggerPort();

        var options = new ChromeOptions();
        options.DebuggerAddress = $"127.0.0.1:{DEBUGGING_PORT}";

        return new ChromeDriver(options);
    }

    private static string GetChromeExecutable()
    {
        return OperatingSystem.IsWindows()
            ? @"C:\Program Files\Google\Chrome\Application\chrome.exe"
            : "google-chrome";
    }

    private static void LaunchChrome(string profileDirectory)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = GetChromeExecutable(),
            Arguments =
                $"--remote-debugging-port={DEBUGGING_PORT} --user-data-dir=\"{profileDirectory}\" --no-first-run --window-size=1400,1000 --use-gl=angle --use-angle=swiftshader",
            UseShellExecute = false
        };

        Process.Start(startInfo);
    }

    private static void WaitForDebuggerPort()
    {
        using HttpClient client = new();
        var ready = false;
        var attempts = 0;

        while (!ready && attempts < 40)
        {
            attempts++;
            Thread.Sleep(500);
            ready = DebuggerResponds(client);
        }

        if (!ready) throw new TimeoutException("Chrome did not open its debugging port.");
    }

    private static bool DebuggerResponds(HttpClient client)
    {
        var result = false;

        try
        {
            using var response = client.GetAsync($"http://127.0.0.1:{DEBUGGING_PORT}/json/version").Result;
            result = response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
        }

        return result;
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