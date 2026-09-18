namespace Automation.Trading;

public static class BiddingStrategy
{
    public const uint MARGIN_COINS = 1000;
    public const uint MIN_AUCTION_MINUTES = 3;
    public const uint MAX_AUCTION_MINUTES = 20;
    public const double MAX_EXPOSURE_SHARE = 0.5;
    public const uint RESALE_WINDOW_DAYS = 7;
    public const int RESALE_SAMPLE_SIZE = 3;
    public const uint RESALE_MAX_AGE_HOURS = 6;
    public const uint OPEN_BID_TIMEOUT_HOURS = 2;
    public const int MIN_COMPARE_LISTINGS = 2;
    public const int MAX_COMPARE_PAGES = 5;
    public const int MAX_COMPARE_PAGES_FOR_LISTING = 1;
    public const uint LOOP_MINUTES_DEFAULT = 10;
    public const byte RESULT_PAGES_TO_SCAN = 3;
    public const byte DEEP_RESULT_PAGES_TO_SCAN = 12;
    public const uint CLUB_ITEM_MAX_BID = 500;
    public const uint CLUB_ITEM_MIN_BUY_NOW = 1000;
    public const uint CALIBRATION_WINDOW_DAYS = 14;
    public const int CALIBRATION_PRIOR_WEIGHT = 5;
    public const double CALIBRATION_MIN_RATIO = 0.5;
    public const double CALIBRATION_MAX_RATIO = 1.2;
    public const int ITEM_SALES_MIN = 2;
    public const double SALES_MAX_UPLIFT = 1.5;
    public const uint SEGMENT_WINDOW_DAYS = 7;
    public const int SEGMENT_MIN_RESOLVED_BIDS = 10;
    public const double SEGMENT_MIN_WIN_RATE = 0.10;
    public const uint SEGMENT_COOLDOWN_HOURS = 6;
    public const uint SEGMENT_PROBE_BIDS = 3;
}
