using Automation.Trading;

namespace Automation.Tests;

public class RowSnapshotTests
{
    private const string JSON = """
        [
          {"index":0,"classes":"listFUTItem has-auction-data outbid","name":"Voll","rating":"72","position":"GK","description":"","itemClasses":"item player silver","time":"<30 Seconds","bid":"1,000","start":"300","buyNow":"10,000",
           "model":{"tradeId":"123","secondsLeft":21,"bidState":"outbid","tradeState":"active","currentBid":1000,"startingBid":300,"name":"Voll","updating":true,"ageMs":4300}},
          {"index":1,"classes":"listFUTItem has-auction-data","name":"Rapid Wien","rating":"","position":"","description":"","itemClasses":"item badge silver","time":"3 Minutes","bid":"---","start":"350","buyNow":"5,000","model":null},
          {"index":2,"classes":"listFUTItem has-auction-data","name":"Marseiler","rating":"70","position":"LM","description":"","itemClasses":"item player","time":"1 Minute","bid":"---","start":"300","buyNow":"10,000",
           "model":{"tradeId":"9","secondsLeft":70,"bidState":"none","tradeState":"active","currentBid":0,"startingBid":550,"name":"Other"}}
        ]
        """;

    private static IReadOnlyList<RowSnapshot> Rows()
    {
        return RowSnapshotParser.Parse(JSON);
    }

    [Fact]
    public void ParsesEveryRowWithItsKeyAndCoinValues()
    {
        var rows = Rows();

        Assert.Equal(3, rows.Count);
        Assert.Equal("Voll 72 GK", rows[0].Key);
        Assert.Equal("Rapid Wien Badge", rows[1].Key);
        Assert.Equal(1000u, rows[0].BidValue);
        Assert.Null(rows[1].BidValue);
        Assert.Equal(10000u, rows[0].BuyNowValue);
        Assert.Equal(0u, rows[0].MinutesLeft);
        Assert.Equal(3u, rows[1].MinutesLeft);
    }

    [Fact]
    public void NextBidIsTheIncrementAboveTheCurrentBidOrTheStartPrice()
    {
        var rows = Rows();

        Assert.Equal(1100u, rows[0].NextBid);
        Assert.Equal(350u, rows[1].NextBid);
    }

    [Fact]
    public void ModelIsTrustedOnlyWhenItsStartPriceMatchesTheRow()
    {
        var rows = Rows();

        Assert.Equal(21, rows[0].TrustedModel?.SecondsLeft);
        Assert.True(rows[0].TrustedModel?.Updating);
        Assert.Equal(4300, rows[0].TrustedModel?.AgeMs);
        Assert.Null(rows[2].Model?.AgeMs);
        Assert.Null(rows[1].TrustedModel);
        Assert.Null(rows[2].TrustedModel);
    }

    [Fact]
    public void MalformedJsonGivesNoRows()
    {
        Assert.Empty(RowSnapshotParser.Parse("not json"));
        Assert.Empty(RowSnapshotParser.Parse(""));
    }

    [Fact]
    public void TheModelSourceNamesAreTheOnesTheSnapshotScriptReads()
    {
        Assert.Equal("none", RowModels.None.ToString().ToLowerInvariant());
        Assert.Equal("watched", RowModels.Watched.ToString().ToLowerInvariant());
        Assert.Equal("results", RowModels.Results.ToString().ToLowerInvariant());
    }

    [Fact]
    public void TheSnapshotScriptReadsBothModelSourcesByName()
    {
        var script = File.ReadAllText(Path.Combine(SourceTree.Root(), "Automation", "Screen.cs"));

        Assert.Contains("source === 'results'", script, StringComparison.Ordinal);
        Assert.Contains("source === 'watched'", script, StringComparison.Ordinal);
        Assert.Contains("paginationViewModel", script, StringComparison.Ordinal);
    }
}
