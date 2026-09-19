using Automation.Catalogue;

namespace Automation.Tests;

public class CataloguePlanTests
{
    [Fact]
    public void AFirstRunStartsAtPageOne()
    {
        Assert.Equal(1, CataloguePlan.StartPage(null));
    }

    [Fact]
    public void AnInterruptedRunCarriesOnAfterItsLastFinishedPage()
    {
        ImportProgress progress = new(7, 12, CataloguePlan.OUTCOME_RUNNING, null);

        Assert.True(CataloguePlan.IsResumable(progress));
        Assert.Equal(13, CataloguePlan.StartPage(progress));
    }

    [Fact]
    public void AFailedRunIsAlsoResumed()
    {
        ImportProgress progress = new(7, 12, CataloguePlan.OUTCOME_FAILED, null);

        Assert.Equal(13, CataloguePlan.StartPage(progress));
    }

    [Fact]
    public void AFinishedRunStartsOverSoTheCatalogueRefreshes()
    {
        ImportProgress progress = new(7, 400, CataloguePlan.OUTCOME_COMPLETE, null);

        Assert.False(CataloguePlan.IsResumable(progress));
        Assert.Equal(1, CataloguePlan.StartPage(progress));
    }

    [Fact]
    public void ARunThatFoundNoChangeIsNotResumed()
    {
        ImportProgress progress = new(7, 1, CataloguePlan.OUTCOME_UNCHANGED, null);

        Assert.False(CataloguePlan.IsResumable(progress));
        Assert.Equal(1, CataloguePlan.StartPage(progress));
    }

    [Fact]
    public void AFetchedPageAddsItsPlayersAndItsTag()
    {
        var advanced = CataloguePlan.Advance(CataloguePlan.Start(), 1, Page(2, 2, "Mon, 14 Sep 2026 23:18:58 GMT"));

        Assert.Equal(1, advanced.PagesFetched);
        Assert.Equal(2, advanced.PlayersSaved);
        Assert.Equal("Mon, 14 Sep 2026 23:18:58 GMT", advanced.Tag);
        Assert.False(advanced.Unchanged);
        Assert.False(advanced.ReachedEnd);
    }

    [Fact]
    public void TheLastPageReachesTheEnd()
    {
        var advanced = CataloguePlan.Advance(CataloguePlan.Start(), 2, Page(2, 2, null));

        Assert.True(advanced.ReachedEnd);
        Assert.Equal(CataloguePlan.OUTCOME_COMPLETE, CataloguePlan.Outcome(advanced));
    }

    [Fact]
    public void AnUnchangedFetchCountsNoPageAndEndsTheRun()
    {
        var advanced = CataloguePlan.Advance(CataloguePlan.Start(), 1, CataloguePlan.UnchangedPage("tag"));

        Assert.Equal(0, advanced.PagesFetched);
        Assert.Equal(0, advanced.PlayersSaved);
        Assert.True(advanced.Unchanged);
        Assert.True(advanced.ReachedEnd);
        Assert.Equal("tag", advanced.Tag);
        Assert.Equal(CataloguePlan.OUTCOME_UNCHANGED, CataloguePlan.Outcome(advanced));
    }

    [Fact]
    public void ARunStoppedByThePageLimitIsStillRunning()
    {
        var advanced = CataloguePlan.Advance(CataloguePlan.Start(), 1, Page(9, 2, null));

        Assert.False(CataloguePlan.ShouldFetchMore(advanced, 1));
        Assert.True(CataloguePlan.ShouldFetchMore(advanced, 0));
        Assert.Equal(CataloguePlan.OUTCOME_RUNNING, CataloguePlan.Outcome(advanced));
    }

    private static CataloguePage Page(int pageTotal, int players, string? tag)
    {
        return new CataloguePage(1, pageTotal, players,
            Enumerable.Range(1, players)
                .Select(index => new PlayerRecord(index, null, null, "Player", null, null, null, [], null, null, null,
                    null))
                .ToList(), tag);
    }

    [Fact]
    public void TheLastPageEndsTheRun()
    {
        Assert.True(CataloguePlan.HasMorePages(3, 9));
        Assert.False(CataloguePlan.HasMorePages(9, 9));
        Assert.False(CataloguePlan.HasMorePages(10, 9));
    }

    [Fact]
    public void APageLimitEndsTheRunEarly()
    {
        Assert.True(CataloguePlan.WithinPageLimit(2, 3));
        Assert.False(CataloguePlan.WithinPageLimit(3, 3));
        Assert.True(CataloguePlan.WithinPageLimit(9999, 0));
    }

    [Fact]
    public void TheFirstPageOfARunIsNotDelayed()
    {
        Assert.Equal(0, CataloguePlan.DelayMs(0, 1500));
        Assert.Equal(1500, CataloguePlan.DelayMs(1, 1500));
    }
}

public class CatalogueImporterTests
{
    [Fact]
    public async Task EveryPageIsFetchedAndSaved()
    {
        StubSource source = new(TwoPages());
        StubStore store = new();
        var importer = Importer(source, store);

        var result = await importer.RunAsync(CancellationToken.None);

        Assert.Equal([1, 2], source.RequestedPages);
        Assert.Equal(3, result.PlayersSaved);
        Assert.Equal(2, result.PagesFetched);
        Assert.Equal(CataloguePlan.OUTCOME_COMPLETE, result.Outcome);
        Assert.Equal([1001L, 1002L, 1003L], store.Saved.Select(player => player.SourceId));
    }

    [Fact]
    public async Task ARunOpensOneImportAndClosesItComplete()
    {
        StubStore store = new();

        var result = await Importer(new StubSource(TwoPages()), store).RunAsync(CancellationToken.None);

        Assert.Equal(1, store.ImportsOpened);
        Assert.Equal(result.ImportId, store.FinishedId);
        Assert.Equal(CataloguePlan.OUTCOME_COMPLETE, store.FinishedOutcome);
        Assert.Equal("page-two-tag", store.FinishedTag);
        Assert.Equal([(1, 2, 2), (2, 2, 1)], store.Progress);
    }

    [Fact]
    public async Task AnInterruptedRunResumesRatherThanStartingAgain()
    {
        StubStore store = new() { Latest = new ImportProgress(42, 1, CataloguePlan.OUTCOME_RUNNING, null) };
        StubSource source = new(TwoPages());

        var result = await Importer(source, store).RunAsync(CancellationToken.None);

        Assert.Equal([2], source.RequestedPages);
        Assert.Equal(0, store.ImportsOpened);
        Assert.Equal(42, result.ImportId);
        Assert.Equal(1, result.PlayersSaved);
    }

    [Fact]
    public async Task APageLimitStopsTheRunWithoutClosingTheImport()
    {
        StubStore store = new();
        StubSource source = new(TwoPages());
        CatalogueImportSettings settings = new(0, 1);

        var result = await new CatalogueImporter(source, store, settings, NoDelay).RunAsync(CancellationToken.None);

        Assert.Equal([1], source.RequestedPages);
        Assert.Equal(CataloguePlan.OUTCOME_RUNNING, result.Outcome);
        Assert.Equal(CataloguePlan.OUTCOME_RUNNING, store.FinishedOutcome);
    }

    [Fact]
    public async Task AFailedPageMarksTheImportFailedAndRaises()
    {
        StubStore store = new();
        StubSource source = new(TwoPages()) { FailOnPage = 2 };

        await Assert.ThrowsAsync<CatalogueRequestException>(() =>
            Importer(source, store).RunAsync(CancellationToken.None));

        Assert.Equal(CataloguePlan.OUTCOME_FAILED, store.FinishedOutcome);
        Assert.Single(store.Progress);
    }

    [Fact]
    public async Task AnUnchangedSourceSavesNothingAndSaysSo()
    {
        StubStore store = new() { Latest = new ImportProgress(42, 1, CataloguePlan.OUTCOME_COMPLETE, "old-tag") };
        StubSource source = new(TwoPages()) { UnchangedFor = "old-tag" };

        var result = await Importer(source, store).RunAsync(CancellationToken.None);

        Assert.Equal("old-tag", source.SeenTag);
        Assert.Empty(store.Saved);
        Assert.Empty(store.Progress);
        Assert.Equal(CataloguePlan.OUTCOME_UNCHANGED, result.Outcome);
        Assert.Equal(CataloguePlan.OUTCOME_UNCHANGED, store.FinishedOutcome);
    }

    [Fact]
    public async Task ThePagesAreSpacedByTheConfiguredDelay()
    {
        List<int> delays = [];
        CatalogueImportSettings settings = new(2500, 0);
        CatalogueImporter importer = new(new StubSource(TwoPages()), new StubStore(), settings,
            (delayMs, _) =>
            {
                delays.Add(delayMs);

                return Task.CompletedTask;
            });

        await importer.RunAsync(CancellationToken.None);

        Assert.Equal([0, 2500], delays);
    }

    private static CatalogueImporter Importer(IPlayerSource source, ICatalogueStore store)
    {
        return new CatalogueImporter(source, store, new CatalogueImportSettings(0, 0), NoDelay);
    }

    private static Task NoDelay(int delayMs, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private static List<CataloguePage> TwoPages()
    {
        return
        [
            new CataloguePage(1, 2, 3, [Player(1001), Player(1002)], "page-one-tag"),
            new CataloguePage(2, 2, 3, [Player(1003)], "page-two-tag")
        ];
    }

    private static PlayerRecord Player(long futDbId)
    {
        return new PlayerRecord(futDbId, futDbId * 10L, futDbId + 200000L, $"Player {futDbId}", null, 80, "ST",
            ["CF"], 1, 2, 3, 4);
    }

    private sealed class StubSource(IReadOnlyList<CataloguePage> pages) : IPlayerSource
    {
        public List<int> RequestedPages { get; } = [];
        public int FailOnPage { get; init; }
        public string? UnchangedFor { get; init; }
        public string? SeenTag { get; private set; }

        public string Name => "stub";

        public Task<CataloguePage> FetchPageAsync(int page, string? tag, CancellationToken cancellationToken)
        {
            RequestedPages.Add(page);
            SeenTag = tag;

            if (page == FailOnPage) throw new CatalogueRequestException("FUT-DB answered 429: slow down");

            return Task.FromResult(tag != null && tag == UnchangedFor
                ? CataloguePlan.UnchangedPage(tag)
                : pages[page - 1]);
        }
    }

    private sealed class StubStore : ICatalogueStore
    {
        public List<PlayerRecord> Saved { get; } = [];
        public List<(int page, int pageTotal, int itemCount)> Progress { get; } = [];
        public ImportProgress? Latest { get; init; }
        public int ImportsOpened { get; private set; }
        public long FinishedId { get; private set; }
        public string FinishedOutcome { get; private set; } = CataloguePlan.OUTCOME_RUNNING;
        public string? FinishedTag { get; private set; }

        public ImportProgress? LatestImport(string source)
        {
            return Latest;
        }

        public long BeginImport(string source)
        {
            ImportsOpened = ImportsOpened + 1;

            return 99;
        }

        public void SavePlayers(string source, IReadOnlyList<PlayerRecord> players)
        {
            Saved.AddRange(players);
        }

        public void RecordProgress(long importId, int page, int pageTotal, int itemCount)
        {
            Progress.Add((page, pageTotal, itemCount));
        }

        public void FinishImport(long importId, string outcome, string? tag)
        {
            FinishedId = importId;
            FinishedOutcome = outcome;
            FinishedTag = tag;
        }
    }
}
