namespace Automation.Trading;

public static class BiddingStrategy
{
    public const uint MARGIN_COINS = 1000;
    public const uint MIN_AUCTION_MINUTES = 2;
    public const uint MAX_AUCTION_MINUTES = 20;
    public const double MAX_EXPOSURE_SHARE = 0.5;
    public const uint RESALE_WINDOW_DAYS = 7;
    public const int RESALE_SAMPLE_SIZE = 3;
    public const uint RESALE_MAX_AGE_HOURS = 6;
    public const uint OPEN_BID_TIMEOUT_HOURS = 2;
    public const uint CLUB_ITEM_MAX_BID = 500;
    public const uint CLUB_ITEM_MIN_BUY_NOW = 1000;
}
