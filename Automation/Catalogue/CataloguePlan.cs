namespace Automation.Catalogue;

public sealed record ImportProgress(long ImportId, int LastPage, string Outcome);

public sealed record CatalogueImportSettings(int PageDelayMs = 1500, int MaxPages = 0);

public sealed record CatalogueImportResult(long ImportId, int PagesFetched, int PlayersSaved, string Outcome);

public static class CataloguePlan
{
    public const string OUTCOME_RUNNING = "running";
    public const string OUTCOME_COMPLETE = "complete";
    public const string OUTCOME_FAILED = "failed";
    private const int FIRST_PAGE = 1;
    private const int NO_PAGE_LIMIT = 0;

    public static bool IsResumable(ImportProgress? progress)
    {
        return progress != null && progress.Outcome != OUTCOME_COMPLETE;
    }

    public static int StartPage(ImportProgress? progress)
    {
        return IsResumable(progress) ? progress!.LastPage + FIRST_PAGE : FIRST_PAGE;
    }

    public static bool HasMorePages(int page, int pageTotal)
    {
        return page < pageTotal;
    }

    public static bool WithinPageLimit(int pagesFetched, int maxPages)
    {
        return maxPages == NO_PAGE_LIMIT || pagesFetched < maxPages;
    }

    public static int DelayMs(int pagesFetched, int pageDelayMs)
    {
        return pagesFetched == 0 ? 0 : pageDelayMs;
    }
}
