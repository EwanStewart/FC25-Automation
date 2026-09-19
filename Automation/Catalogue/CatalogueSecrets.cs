using UtilityFunctions = Automation.Utility.Utility;

namespace Automation.Catalogue;

public static class CatalogueSecrets
{
    public const string FUT_DB_KEY = "FUT_DB_KEY";

    public static string ApiKey()
    {
        return RequireApiKey(UtilityFunctions.GetSecret(FUT_DB_KEY));
    }

    public static string RequireApiKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new CatalogueConfigurationException(
                $"The player catalogue import needs an API key. Add {FUT_DB_KEY} to the .env file at the root of the repository.");

        return value;
    }
}
