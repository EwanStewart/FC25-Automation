namespace Automation.Sbc.Fulfilment;

public static class CardCeiling
{
    public const int MARGIN_PERCENT = 25;
    public const int MARGIN_MAX_COINS = 1000;
    public const int MINIMUM_COINS = 250;

    public static int For(int estimate)
    {
        var margin = Math.Min(estimate * MARGIN_PERCENT / 100, MARGIN_MAX_COINS);

        return Math.Max(MINIMUM_COINS, estimate + margin);
    }

    public static int Affordable(int cardCeiling, SpendLedger ledger, int slot)
    {
        var headroom = ledger.Ceiling - ledger.Committed + ledger.Standing(slot);

        return (int)Math.Max(0, Math.Min(cardCeiling, headroom));
    }
}
