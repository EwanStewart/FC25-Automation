namespace Automation.Catalogue;

public interface IPlayerSource
{
    string Name { get; }

    Task<CataloguePage> FetchPageAsync(int page, CancellationToken cancellationToken);
}
