namespace Automation.Catalogue;

public static class CatalogueProgram
{
    public const int PAGE_DELAY_MS = 1500;
    public const int MAX_PAGES_PER_RUN = 0;
    private const int SUCCESS = 0;
    private const int FAILURE = 1;

    public static int Run()
    {
        var result = FAILURE;

        try
        {
            result = Import();
        }
        catch (CatalogueException error)
        {
            Console.WriteLine(error.Message);
        }

        return result;
    }

    private static int Import()
    {
        using HttpClient client = new();
        FutDbPlayerSource source = new(client, CatalogueSecrets.ApiKey());
        CatalogueImporter importer = new(source, new MySqlCatalogueStore(),
            new CatalogueImportSettings(PAGE_DELAY_MS, MAX_PAGES_PER_RUN));
        Report(importer.RunAsync(CancellationToken.None).GetAwaiter().GetResult());

        return SUCCESS;
    }

    private static void Report(CatalogueImportResult result)
    {
        Console.WriteLine(
            $"Catalogue import {result.ImportId} {result.Outcome}: {result.PlayersSaved} players over {result.PagesFetched} pages.");
    }
}
