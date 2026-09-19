using Automation.Trading;

namespace Automation.Tests;

public class RunOptionsTests
{
    [Fact]
    public void NoArgumentsMeansAFullPassThatShutsDownAfterwards()
    {
        var options = RunOptions.Parse([]);

        Assert.False(options.SmokeTest);
        Assert.Equal("all", options.SmokeTarget);
        Assert.False(options.NoShutdown);
        Assert.Equal(0u, options.LoopMinutes);
        Assert.False(options.SnipeOnly);
    }

    [Fact]
    public void SnipeRunsTheSnipePassAlone()
    {
        Assert.True(RunOptions.Parse(["--snipe"]).SnipeOnly);
        Assert.True(RunOptions.Parse(["--no-shutdown", "--snipe"]).NoShutdown);
    }

    [Fact]
    public void SmokeTakesAnOptionalTarget()
    {
        Assert.Equal("players", RunOptions.Parse(["--smoke", "players"]).SmokeTarget);
        Assert.Equal("all", RunOptions.Parse(["--smoke"]).SmokeTarget);
        Assert.Equal("all", RunOptions.Parse(["--smoke", "--no-shutdown"]).SmokeTarget);
    }

    [Fact]
    public void LoopTakesMinutesAndFallsBackToTheDefault()
    {
        Assert.Equal(5u, RunOptions.Parse(["--loop", "5"]).LoopMinutes);
        Assert.Equal(BiddingStrategy.LOOP_MINUTES_DEFAULT, RunOptions.Parse(["--loop"]).LoopMinutes);
        Assert.Equal(BiddingStrategy.LOOP_MINUTES_DEFAULT, RunOptions.Parse(["--loop", "soon"]).LoopMinutes);
    }

    [Fact]
    public void FulfilmentIsOffUntilAskedForAndDryUntilAskedTwice()
    {
        Assert.False(RunOptions.Parse(["--no-shutdown"]).FulfilSbc);
        Assert.True(RunOptions.Parse(["--fulfil-sbc", "--no-shutdown"]).FulfilSbc);
        Assert.False(RunOptions.Parse(["--fulfil-sbc"]).FulfilLive);
        Assert.True(RunOptions.Parse(["--fulfil-sbc", "--fulfil-live"]).FulfilLive);
    }
}
