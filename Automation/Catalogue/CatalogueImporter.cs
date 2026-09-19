namespace Automation.Catalogue;

public sealed class CatalogueImporter
{
    private readonly IPlayerSource source_;
    private readonly ICatalogueStore store_;
    private readonly CatalogueImportSettings settings_;
    private readonly Func<int, CancellationToken, Task> delay_;

    public CatalogueImporter(IPlayerSource source, ICatalogueStore store, CatalogueImportSettings settings)
        : this(source, store, settings, Wait)
    {
    }

    public CatalogueImporter(IPlayerSource source, ICatalogueStore store, CatalogueImportSettings settings,
        Func<int, CancellationToken, Task> delay)
    {
        source_ = source;
        store_ = store;
        settings_ = settings;
        delay_ = delay;
    }

    public async Task<CatalogueImportResult> RunAsync(CancellationToken cancellationToken)
    {
        var previous = store_.LatestImport(source_.Name);
        var importId = CataloguePlan.IsResumable(previous) ? previous!.ImportId : store_.BeginImport(source_.Name);

        return await GuardedRunAsync(importId, CataloguePlan.StartPage(previous), cancellationToken);
    }

    private async Task<CatalogueImportResult> GuardedRunAsync(long importId, int startPage,
        CancellationToken cancellationToken)
    {
        CatalogueImportResult result;

        try
        {
            result = await FetchPagesAsync(importId, startPage, cancellationToken);
        }
        catch (CatalogueException)
        {
            store_.FinishImport(importId, CataloguePlan.OUTCOME_FAILED);

            throw;
        }

        return result;
    }

    private async Task<CatalogueImportResult> FetchPagesAsync(long importId, int startPage,
        CancellationToken cancellationToken)
    {
        var page = startPage;
        var pagesFetched = 0;
        var playersSaved = 0;
        var moreToFetch = true;

        while (moreToFetch && CataloguePlan.WithinPageLimit(pagesFetched, settings_.MaxPages))
        {
            await delay_(CataloguePlan.DelayMs(pagesFetched, settings_.PageDelayMs), cancellationToken);
            var fetched = await ImportPageAsync(importId, page, cancellationToken);
            pagesFetched += 1;
            playersSaved += fetched.Players.Count;
            moreToFetch = CataloguePlan.HasMorePages(page, fetched.PageTotal);
            page += 1;
        }

        return Close(importId, pagesFetched, playersSaved, moreToFetch);
    }

    private async Task<CataloguePage> ImportPageAsync(long importId, int page, CancellationToken cancellationToken)
    {
        var fetched = await source_.FetchPageAsync(page, cancellationToken);
        store_.SavePlayers(source_.Name, fetched.Players);
        store_.RecordProgress(importId, page, fetched.PageTotal, fetched.Players.Count);

        return fetched;
    }

    private CatalogueImportResult Close(long importId, int pagesFetched, int playersSaved, bool moreToFetch)
    {
        var outcome = moreToFetch ? CataloguePlan.OUTCOME_RUNNING : CataloguePlan.OUTCOME_COMPLETE;

        if (!moreToFetch) store_.FinishImport(importId, outcome);

        return new CatalogueImportResult(importId, pagesFetched, playersSaved, outcome);
    }

    private static Task Wait(int delayMs, CancellationToken cancellationToken)
    {
        return delayMs > 0 ? Task.Delay(delayMs, cancellationToken) : Task.CompletedTask;
    }
}
