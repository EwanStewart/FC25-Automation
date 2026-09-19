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
        ImportProgress progress = new(7, 12, CataloguePlan.OUTCOME_RUNNING);

        Assert.True(CataloguePlan.IsResumable(progress));
        Assert.Equal(13, CataloguePlan.StartPage(progress));
    }

    [Fact]
    public void AFailedRunIsAlsoResumed()
    {
        ImportProgress progress = new(7, 12, CataloguePlan.OUTCOME_FAILED);

        Assert.Equal(13, CataloguePlan.StartPage(progress));
    }

    [Fact]
    public void AFinishedRunStartsOverSoTheCatalogueRefreshes()
    {
        ImportProgress progress = new(7, 400, CataloguePlan.OUTCOME_COMPLETE);

        Assert.False(CataloguePlan.IsResumable(progress));
        Assert.Equal(1, CataloguePlan.StartPage(progress));
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
        Assert.Equal([1001, 1002, 1003], store.Saved.Select(player => player.DefinitionId));
    }

    [Fact]
    public async Task ARunOpensOneImportAndClosesItComplete()
    {
        StubStore store = new();

        var result = await Importer(new StubSource(TwoPages()), store).RunAsync(CancellationToken.None);

        Assert.Equal(1, store.ImportsOpened);
        Assert.Equal(result.ImportId, store.FinishedId);
        Assert.Equal(CataloguePlan.OUTCOME_COMPLETE, store.FinishedOutcome);
        Assert.Equal([(1, 2, 2), (2, 2, 1)], store.Progress);
    }

    [Fact]
    public async Task AnInterruptedRunResumesRatherThanStartingAgain()
    {
        StubStore store = new() { Latest = new ImportProgress(42, 1, CataloguePlan.OUTCOME_RUNNING) };
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
            new CataloguePage(1, 2, 3, [Player(1001), Player(1002)]),
            new CataloguePage(2, 2, 3, [Player(1003)])
        ];
    }

    private static PlayerRecord Player(int definitionId)
    {
        return new PlayerRecord(definitionId, definitionId * 10L, $"Player {definitionId}", null, 80, "ST", ["CF"], 1,
            2, 3, 4, "gold");
    }

    private sealed class StubSource(IReadOnlyList<CataloguePage> pages) : IPlayerSource
    {
        public List<int> RequestedPages { get; } = [];
        public int FailOnPage { get; init; }

        public string Name => "stub";

        public Task<CataloguePage> FetchPageAsync(int page, CancellationToken cancellationToken)
        {
            RequestedPages.Add(page);

            if (page == FailOnPage) throw new CatalogueRequestException("FutDB answered 429: slow down");

            return Task.FromResult(pages[page - 1]);
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

        public ImportProgress? LatestImport(string source)
        {
            return Latest;
        }

        public long BeginImport(string source)
        {
            ImportsOpened = ImportsOpened + 1;

            return 99;
        }

        public void SavePlayers(IReadOnlyList<PlayerRecord> players)
        {
            Saved.AddRange(players);
        }

        public void RecordProgress(long importId, int page, int pageTotal, int itemCount)
        {
            Progress.Add((page, pageTotal, itemCount));
        }

        public void FinishImport(long importId, string outcome)
        {
            FinishedId = importId;
            FinishedOutcome = outcome;
        }
    }
}
