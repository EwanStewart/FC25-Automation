using Automation.Trading;

namespace Automation.Tests;

public class BackoffTests
{
    [Fact]
    public void OrdinaryFailuresLeaveThePassRunning()
    {
        Assert.Equal(BackoffAction.Continue, Backoff.Decide(Array.Empty<int>()));
        Assert.Equal(BackoffAction.Continue, Backoff.Decide(new[] { 401, 461, 521, 500 }));
    }

    [Theory]
    [InlineData(429)]
    [InlineData(512)]
    public void TheFirstThrottleSlowsThePassAndTheSecondEndsIt(int status)
    {
        Assert.Equal(BackoffAction.SlowDown, Backoff.Decide(new[] { 401, status }));
        Assert.Equal(BackoffAction.EndPass, Backoff.Decide(new[] { status, 461, 429 }));
    }

    [Theory]
    [InlineData(458)]
    [InlineData(426)]
    [InlineData(494)]
    public void MarketLockCodesStopTheRunWhateverElseWasSeen(int status)
    {
        Assert.Equal(BackoffAction.StopRun, Backoff.Decide(new[] { 429, 429, status }));
        Assert.Equal(BackoffAction.StopRun, Backoff.Decide(new[] { status }));
    }
}
