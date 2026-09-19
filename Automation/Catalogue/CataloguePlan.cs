namespace Automation.Catalogue;

public sealed record ImportProgress(long ImportId, int LastPage, string Outcome, string? Tag);

public sealed record CatalogueImportSettings(int PageDelayMs = 1500, int MaxPages = 0);

public sealed record CatalogueImportResult(long ImportId, int PagesFetched, int PlayersSaved, string Outcome);

public sealed record FetchProgress(int PagesFetched, int PlayersSaved, bool Unchanged, bool ReachedEnd, string? Tag);

public static class CataloguePlan
{
    public const string OUTCOME_RUNNING = "running";
    public const string OUTCOME_COMPLETE = "complete";
    public const string OUTCOME_UNCHANGED = "unchanged";
    public const string OUTCOME_FAILED = "failed";
    private const int FIRST_PAGE = 1;
    private const int NO_PAGE_LIMIT = 0;

    public static bool IsResumable(ImportProgress? progress)
    {
        return progress != null && progress.Outcome != OUTCOME_COMPLETE && progress.Outcome != OUTCOME_UNCHANGED;
    }

    public static int StartPage(ImportProgress? progress)
    {
        return IsResumable(progress) ? progress!.LastPage + FIRST_PAGE : FIRST_PAGE;
    }

    public static CataloguePage UnchangedPage(string? tag)
    {
        return new CataloguePage(FIRST_PAGE, FIRST_PAGE, 0, [], tag, true);
    }

    public static FetchProgress Start()
    {
        return new FetchProgress(0, 0, false, false, null);
    }

    public static FetchProgress Advance(FetchProgress progress, int page, CataloguePage fetched)
    {
        return new FetchProgress(progress.PagesFetched + (fetched.Unchanged ? 0 : 1),
            progress.PlayersSaved + fetched.Players.Count, fetched.Unchanged,
            fetched.Unchanged || !HasMorePages(page, fetched.PageTotal), fetched.Tag ?? progress.Tag);
    }

    public static bool ShouldFetchMore(FetchProgress progress, int maxPages)
    {
        return !progress.ReachedEnd && WithinPageLimit(progress.PagesFetched, maxPages);
    }

    public static string Outcome(FetchProgress progress)
    {
        return progress.Unchanged ? OUTCOME_UNCHANGED : progress.ReachedEnd ? OUTCOME_COMPLETE : OUTCOME_RUNNING;
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
