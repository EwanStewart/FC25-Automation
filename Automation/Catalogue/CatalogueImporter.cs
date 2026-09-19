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

        return await GuardedRunAsync(importId, CataloguePlan.StartPage(previous), previous?.Tag, cancellationToken);
    }

    private async Task<CatalogueImportResult> GuardedRunAsync(long importId, int startPage, string? tag,
        CancellationToken cancellationToken)
    {
        CatalogueImportResult result;

        try
        {
            result = await FetchPagesAsync(importId, startPage, tag, cancellationToken);
        }
        catch (CatalogueException)
        {
            store_.FinishImport(importId, CataloguePlan.OUTCOME_FAILED, tag);

            throw;
        }

        return result;
    }

    private async Task<CatalogueImportResult> FetchPagesAsync(long importId, int startPage, string? tag,
        CancellationToken cancellationToken)
    {
        var page = startPage;
        var progress = CataloguePlan.Start();
        var fetching = true;

        while (fetching)
        {
            await delay_(CataloguePlan.DelayMs(progress.PagesFetched, settings_.PageDelayMs), cancellationToken);
            var fetched = await ImportPageAsync(importId, page, tag, cancellationToken);
            progress = CataloguePlan.Advance(progress, page, fetched);
            fetching = CataloguePlan.ShouldFetchMore(progress, settings_.MaxPages);
            page += 1;
        }

        return Close(importId, progress);
    }

    private async Task<CataloguePage> ImportPageAsync(long importId, int page, string? tag,
        CancellationToken cancellationToken)
    {
        var fetched = await source_.FetchPageAsync(page, tag, cancellationToken);
        SavePage(importId, page, fetched);

        return fetched;
    }

    private void SavePage(long importId, int page, CataloguePage fetched)
    {
        if (!fetched.Unchanged)
        {
            store_.SavePlayers(source_.Name, fetched.Players);
            store_.RecordProgress(importId, page, fetched.PageTotal, fetched.Players.Count);
        }
    }

    private CatalogueImportResult Close(long importId, FetchProgress progress)
    {
        var outcome = CataloguePlan.Outcome(progress);

        if (progress.ReachedEnd) store_.FinishImport(importId, outcome, progress.Tag);

        return new CatalogueImportResult(importId, progress.PagesFetched, progress.PlayersSaved, outcome);
    }

    private static Task Wait(int delayMs, CancellationToken cancellationToken)
    {
        return delayMs > 0 ? Task.Delay(delayMs, cancellationToken) : Task.CompletedTask;
    }
}
