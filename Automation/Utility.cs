using System.Diagnostics;
using System.Text.Json;

namespace Automation.Utility;

public static class Utility
{
    /// <summary>
    /// Read All JSON from a specified path.
    /// </summary>
    /// <param name="filePath"> Path not found. </param>
    /// <returns> Found JSON.</returns>
    /// <exception cref="FileNotFoundException"> If path does not exist. </exception>
    public static string ReadJson(string filePath)
    {
        string? returnJson;

        if (File.Exists(filePath))
            returnJson = File.ReadAllText(filePath);
        else
            throw new FileNotFoundException($"The file {filePath} was not found.");

        if (returnJson == null) throw new JsonException($"Failed to read JSON file {filePath}.");

        return returnJson;
    }

    /// <summary>
    /// Get the key value from a given JSON string.
    /// </summary>
    /// <param name="jsonString"> JSON string to parse. </param>
    /// <param name="key"> Key to retrieve. </param>
    /// <returns> Key value requested. </returns>
    /// <exception cref="Exception"> Key missing from JSON. </exception>
    public static string GetJsonValue(string jsonString, string key)
    {
        string? returnValue;

        try
        {
            using (var document = JsonDocument.Parse(jsonString))
            {
                // Try to get the property based on the provided key
                if (document.RootElement.TryGetProperty(key, out var value))
                    returnValue = value.GetString();
                else
                    throw new Exception($"Key '{key}' not found in the JSON.");
            }
        }
        catch (JsonException ex)
        {
            throw new Exception("Invalid JSON format.", ex);
        }

        if (returnValue == null) throw new JsonException($"Failed to read key file {key}.");

        return returnValue;
    }

    /// <summary>
    /// Convert a comma seperated number to uint.
    /// </summary>
    /// <param name="stringNumber"> String to convert. </param>
    /// <returns> String as uint. </returns>
    /// <exception cref="FormatException"> String is invalid for conversion to uint. </exception>
    public static uint CommaSeperatedNumberToUInt(string stringNumber)
    {
        stringNumber = stringNumber.Replace(",", "");

        if (!uint.TryParse(stringNumber, out var convertedString))
            throw new FormatException($"Failed to convert {stringNumber}.");

        return convertedString;
    }

    public static void ShutdownPc()
    {
        ProcessStartInfo processInfo = new();

        processInfo.FileName = "shutdown.exe";

        processInfo.Arguments = "/s /t 60";

        Process.Start(processInfo);
    }

    public static void RetryAction(Action action, int maxRetries = 3, int delayMilliseconds = 5000)
    {
        var retryCount = 0;
        var success = false;

        while (retryCount < maxRetries && !success)
            try
            {
                action.Invoke();
                success = true;
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.Message);
                retryCount++;
                if (retryCount < maxRetries)
                    Thread.Sleep(delayMilliseconds);
                else
                    return;
            }
    }
}