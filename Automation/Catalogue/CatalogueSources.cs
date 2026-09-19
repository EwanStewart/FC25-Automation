namespace Automation.Catalogue;

public static class CatalogueSources
{
    public const string DEFAULT_SOURCE = EaPlayerSource.SOURCE_NAME;

    public static IPlayerSource Create(string name, HttpClient client, string? apiKey = null)
    {
        IPlayerSource result;

        if (name == EaPlayerSource.SOURCE_NAME) result = new EaPlayerSource(client);
        else if (name == FutDbPlayerSource.SOURCE_NAME) result = new FutDbPlayerSource(client, apiKey ?? string.Empty);
        else
            throw new CatalogueConfigurationException(
                $"There is no catalogue source called {name}. The sources are {EaPlayerSource.SOURCE_NAME} and {FutDbPlayerSource.SOURCE_NAME}.");

        return result;
    }
}
