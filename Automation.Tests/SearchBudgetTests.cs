using Automation.Trading;

namespace Automation.Tests;

public class SearchBudgetTests
{
    private static readonly DateTime Now = new(2026, 9, 18, 21, 0, 0, DateTimeKind.Utc);

    private static IEnumerable<DateTime> Every(double seconds, int count, double firstAgoSeconds)
    {
        return Enumerable.Range(0, count).Select(index => Now.AddSeconds(-firstAgoSeconds - index * seconds));
    }

    [Fact]
    public void UnderBothCapsThereIsNoWait()
    {
        Assert.Equal(0, SearchBudget.WaitSeconds(Every(3, 19, 1), Now, 20, 300));
        Assert.Equal(0, SearchBudget.WaitSeconds(Array.Empty<DateTime>(), Now, 20, 300));
    }

    [Fact]
    public void AtTheMinuteCapTheWaitEndsWhenTheOldestSearchLeavesTheWindow()
    {
        var searches = Every(2, 20, 1).ToList();

        Assert.Equal(21, SearchBudget.WaitSeconds(searches, Now, 20, 300));
        Assert.Equal(0, SearchBudget.WaitSeconds(searches, Now.AddSeconds(21), 20, 300));
    }

    [Fact]
    public void SearchesOlderThanAMinuteDoNotCount()
    {
        Assert.Equal(0, SearchBudget.WaitSeconds(Every(1, 25, 61), Now, 20, 300));
    }

    [Fact]
    public void TheHourCapHoldsEvenWhenTheMinuteIsQuiet()
    {
        var searches = Every(10, 300, 120).ToList();

        Assert.Equal(1 * 60 * 60 - 120 - 299 * 10, SearchBudget.WaitSeconds(searches, Now, 20, 300));
        Assert.Equal(0, SearchBudget.WaitSeconds(searches, Now, 20, 301));
    }

    [Fact]
    public void FutureTimestampsAreIgnored()
    {
        Assert.Equal(0, SearchBudget.WaitSeconds(Enumerable.Repeat(Now.AddSeconds(5), 40), Now, 20, 300));
    }
}
