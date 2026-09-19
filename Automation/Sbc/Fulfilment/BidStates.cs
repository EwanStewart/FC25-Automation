namespace Automation.Sbc.Fulfilment;

public static class BidStates
{
    public const string ACTIVE = "active";
    public const string HIGHEST = "highest";
    public const string BOUGHT = "buyNow";

    public static bool Held(string bidState)
    {
        return bidState == HIGHEST || bidState == BOUGHT;
    }
}
