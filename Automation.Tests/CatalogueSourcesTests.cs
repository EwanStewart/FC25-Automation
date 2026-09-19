using Automation.Catalogue;

namespace Automation.Tests;

public class CatalogueSourcesTests
{
    [Fact]
    public void TheEaWebAppIsTheDefaultSource()
    {
        Assert.Equal(EaPlayerSource.SOURCE_NAME, CatalogueSources.DEFAULT_SOURCE);
    }

    [Fact]
    public void TheDefaultSourceNeedsNoKey()
    {
        using HttpClient client = new();

        Assert.Equal(EaPlayerSource.SOURCE_NAME, CatalogueSources.Create(CatalogueSources.DEFAULT_SOURCE, client).Name);
    }

    [Fact]
    public void FutDbStaysSelectableForPerCardAttributes()
    {
        using HttpClient client = new();

        Assert.IsType<FutDbPlayerSource>(CatalogueSources.Create(FutDbPlayerSource.SOURCE_NAME, client, "abc123"));
    }

    [Fact]
    public void FutDbWithoutAKeyFailsByName()
    {
        using HttpClient client = new();

        var error = Assert.Throws<CatalogueConfigurationException>(() =>
            CatalogueSources.Create(FutDbPlayerSource.SOURCE_NAME, client, null));

        Assert.Contains("FUT_DB_KEY", error.Message);
    }

    [Fact]
    public void AnUnknownSourceNamesWhatIsAvailable()
    {
        using HttpClient client = new();

        var error = Assert.Throws<CatalogueConfigurationException>(() => CatalogueSources.Create("futbin", client));

        Assert.Contains("futbin", error.Message);
        Assert.Contains(EaPlayerSource.SOURCE_NAME, error.Message);
        Assert.Contains(FutDbPlayerSource.SOURCE_NAME, error.Message);
    }
}
