namespace Automation.Trading;

public static class SalesFeedback
{
    public static double CalibrationRatio(IEnumerable<(uint sold, uint estimate)> sales, int priorWeight,
        double minRatio, double maxRatio)
    {
        var ratios = sales
            .Where(sale => sale.estimate > 0)
            .Select(sale => (double)sale.sold / sale.estimate)
            .OrderBy(ratio => ratio)
            .ToList();
        var result = 1.0;

        if (ratios.Count > 0)
            result = (Median(ratios) * ratios.Count + priorWeight) / (ratios.Count + priorWeight);

        return Math.Clamp(result, minRatio, maxRatio);
    }

    public static uint Calibrate(uint askEstimate, double ratio)
    {
        return (uint)Math.Round(askEstimate * ratio);
    }

    public static uint? Resale(IReadOnlyList<uint> sales, uint? askEstimate, int minSales, double maxUplift)
    {
        uint? result = askEstimate;

        if (sales.Count > 0) result = SalesResale(sales, askEstimate, minSales, maxUplift);

        return result;
    }

    private static uint SalesResale(IReadOnlyList<uint> sales, uint? askEstimate, int minSales, double maxUplift)
    {
        var median = Pricing.Median(sales.OrderBy(price => price).ToList());
        var result = median;

        if (askEstimate.HasValue && sales.Count < minSales) result = (median + askEstimate.Value) / 2;
        if (askEstimate.HasValue) result = Math.Min(result, (uint)Math.Round(askEstimate.Value * maxUplift));

        return result;
    }

    private static double Median(IReadOnlyList<double> sorted)
    {
        var middle = sorted.Count / 2;
        var isEven = sorted.Count % 2 == 0;

        return isEven ? (sorted[middle - 1] + sorted[middle]) / 2 : sorted[middle];
    }
}
