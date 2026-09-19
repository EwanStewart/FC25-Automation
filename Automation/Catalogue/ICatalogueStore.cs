namespace Automation.Catalogue;

public interface ICatalogueStore
{
    ImportProgress? LatestImport(string source);

    long BeginImport(string source);

    void SavePlayers(string source, IReadOnlyList<PlayerRecord> players);

    void RecordProgress(long importId, int page, int pageTotal, int itemCount);

    void FinishImport(long importId, string outcome);
}
