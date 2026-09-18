using Automation.Trading;

namespace Automation.Tests;

public class SalesFeedbackTests
{
    [Fact]
    public void CalibrationRatioIsOneWithoutSales()
    {
        Assert.Equal(1.0, SalesFeedback.CalibrationRatio(Array.Empty<(uint, uint)>(), 5, 0.5, 1.2));
    }

    [Fact]
    public void CalibrationRatioShrinksTowardsOneWithFewSales()
    {
        var oneSale = new[] { (2900u, 2000u) };

        Assert.Equal(1.075, SalesFeedback.CalibrationRatio(oneSale, 5, 0.5, 1.2), 3);
    }

    [Fact]
    public void CalibrationRatioUsesTheMedianRatioAndIgnoresZeroEstimates()
    {
        var sales = new[] { (500u, 1000u), (600u, 1000u), (5000u, 1000u), (300u, 0u) };

        Assert.Equal((0.6 * 3 + 5) / 8, SalesFeedback.CalibrationRatio(sales, 5, 0.5, 1.2), 6);
    }

    [Fact]
    public void CalibrationRatioIsClampedToTheBounds()
    {
        var low = Enumerable.Repeat((200u, 1000u), 100).ToList();
        var high = Enumerable.Repeat((3000u, 1000u), 100).ToList();

        Assert.Equal(0.5, SalesFeedback.CalibrationRatio(low, 5, 0.5, 1.2));
        Assert.Equal(1.2, SalesFeedback.CalibrationRatio(high, 5, 0.5, 1.2));
    }

    [Fact]
    public void CalibrateScalesTheAskEstimate()
    {
        Assert.Equal(1800u, SalesFeedback.Calibrate(2000, 0.9));
        Assert.Equal(2150u, SalesFeedback.Calibrate(2000, 1.075));
    }

    [Fact]
    public void ResaleFallsBackToTheAskEstimateWithoutSales()
    {
        Assert.Equal(2000u, SalesFeedback.Resale(Array.Empty<uint>(), 2000, 2, 1.5));
        Assert.Null(SalesFeedback.Resale(Array.Empty<uint>(), null, 2, 1.5));
    }

    [Fact]
    public void ResaleUsesTheMedianSaleWhenEnoughSalesExist()
    {
        Assert.Equal(1500u, SalesFeedback.Resale(new uint[] { 1400, 1600, 1500 }, 2000, 2, 1.5));
        Assert.Equal(1500u, SalesFeedback.Resale(new uint[] { 1400, 1600 }, null, 2, 1.5));
    }

    [Fact]
    public void ResaleBlendsASingleSaleWithTheAskEstimate()
    {
        Assert.Equal(2450u, SalesFeedback.Resale(new uint[] { 2900 }, 2000, 2, 1.5));
        Assert.Equal(2900u, SalesFeedback.Resale(new uint[] { 2900 }, null, 2, 1.5));
    }

    [Fact]
    public void ResaleNeverRisesMoreThanTheUpliftCapAboveTheAskEstimate()
    {
        Assert.Equal(3000u, SalesFeedback.Resale(new uint[] { 10000 }, 2000, 2, 1.5));
        Assert.Equal(3000u, SalesFeedback.Resale(new uint[] { 9000, 10000 }, 2000, 2, 1.5));
    }
}
