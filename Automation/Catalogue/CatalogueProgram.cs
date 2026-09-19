namespace Automation.Catalogue;

public static class CatalogueProgram
{
    public const int PAGE_DELAY_MS = 1500;
    public const int MAX_PAGES_PER_RUN = 0;
    private const int SUCCESS = 0;
    private const int FAILURE = 1;

    public static int Run()
    {
        return Run(CatalogueSources.DEFAULT_SOURCE);
    }

    public static int Run(string sourceName)
    {
        var result = FAILURE;

        try
        {
            result = Import(sourceName);
        }
        catch (CatalogueException error)
        {
            Console.WriteLine(error.Message);
        }

        return result;
    }

    private static int Import(string sourceName)
    {
        using HttpClient client = new();
        var source = CatalogueSources.Create(sourceName, client, ApiKeyFor(sourceName));
        CatalogueImporter importer = new(source, new MySqlCatalogueStore(),
            new CatalogueImportSettings(PAGE_DELAY_MS, MAX_PAGES_PER_RUN));
        Report(importer.RunAsync(CancellationToken.None).GetAwaiter().GetResult());

        return SUCCESS;
    }

    private static string? ApiKeyFor(string sourceName)
    {
        return sourceName == FutDbPlayerSource.SOURCE_NAME ? CatalogueSecrets.ApiKey() : null;
    }

    private static void Report(CatalogueImportResult result)
    {
        Console.WriteLine(
            $"Catalogue import {result.ImportId} {result.Outcome}: {result.PlayersSaved} players over {result.PagesFetched} pages.");
    }
}
