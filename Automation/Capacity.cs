namespace Automation.Trading;

public static class Capacity
{
    public static bool CanBid(uint targets, uint maxTargets, uint listed, uint maxListed, uint bidsPlaced, uint maxBids)
    {
        return targets < maxTargets && listed < maxListed && bidsPlaced < maxBids;
    }

    public static bool WithinBudget(DateTime started, DateTime now, int budgetSeconds)
    {
        return (now - started).TotalSeconds < budgetSeconds;
    }
}
