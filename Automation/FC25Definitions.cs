namespace Automation.Definitions;

public static class Fc25Definitions
{
    public struct Filter
    {
        public string? Quality { get; set; }
        public string? Rarity { get; set; }
        public string? Position { get; set; }
        public string? ChemistryStyle { get; set; }
        public string? Nationality { get; set; }
        public string? League { get; set; }
        public string? Club { get; set; }
        public string? Playstyles { get; set; }
        public uint MaxBidPrice { get; set; }
        public uint MinBuyPrice { get; set; }
    }

    public enum ElementKeys
    {
        INITIAL_LOGIN,
        SECOND_LOGIN,
        PASSWORD_INPUT,
        LEFT_HAND_PANE_TRANSFERS,
        TRANSFER_TARGETS,
        CLEAR_NOT_WON_TRANSFER_TARGETS,
        WON_TARGET,
        SEND_TO_TRANSFER_LIST,
        CLEAR_SOLD_TRANSFERS,
        TRANSFER_LIST,
        LISTABLE_ITEMS,
        COMPARE_PRICE,
        COMPARE_PRICE_ITEMS,
        COMPARE_PRICE_BUY_NOW,
        LIST_ITEM_PRE_PRICE,
        MIN_PRICE_LIST_ITEM,
        MAX_PRICE_LIST_ITEM,
        LIST_ITEM,
        TRANSFER_MARKET,
        CLUB_ITEMS_TRANSFER_MARKET,
        PLAYER_ITEMS_TRANSFER_MARKET,
        QUALITY_DROPDOWN,
        NATIONALITY_DROPDOWN,
        QUALITY_DROPDOWN_SILVER,
        CLUB_ITEMS_TYPE_DROPDOWN,
        CLUB_ITEMS_TYPE_DROPDOWN_BADGES,
        CLUB_ITEMS_TYPE_DROPDOWN_KITS,
        FIND_ALL_PRICE_INPUTS,
        SEARCH,
        AUCTION_ITEMS,
        COMPARE_PRICE_BACK_BUTTON,
        RESET,
        MAKE_BID,
        NEXT,
        TRANSFER_TARGETS_TOTAL,
        RARITY_DROPDOWN,
        MANAGER_ITEMS_TRANSFER_MARKET,
        CONTINUE,
        COMPARE_PRICE_LIST,
        MAX_BID_PRICE_INPUT,
        MIN_BUY_NOW_PRICE_INPUT,
        MAX_BUY_NOW_PRICE_INPUT,
        COIN_TOTAL,
        ITEM_NAME,
        ITEM_TIME_REMAINING
    }

    public static Dictionary<ElementKeys, (string, string)> Elements = new()
    {
        {
            ElementKeys.INITIAL_LOGIN,
            ("#Login button.btn-standard.primary", "Initial Login Button")
        },
        { ElementKeys.PASSWORD_INPUT, ("#password", "Password Input") },
        { ElementKeys.SECOND_LOGIN, ("//*[@id=\"logInBtn\"]", "Second Login Button") },
        {
            ElementKeys.LEFT_HAND_PANE_TRANSFERS,
            ("//nav//button[contains(@class, 'icon-transfer')]", "Transfers Button (Left-Hand Pane)")
        },
        {
            ElementKeys.TRANSFER_TARGETS,
            ("//div[contains(@class, 'ut-tile-transfer-targets')]", "Transfer Targets Button")
        },
        {
            ElementKeys.CLEAR_NOT_WON_TRANSFER_TARGETS,
            ("//button[text()='Clear Expired']", "Clear Not Won Transfers Button")
        },
        {
            ElementKeys.WON_TARGET,
            ("//li[contains(@class, 'listFUTItem') and contains(@class, 'has-auction-data') and contains(@class, 'won')]",
                "Won Target")
        },
        {
            ElementKeys.SEND_TO_TRANSFER_LIST,
            ("//button[.//span[@class='btn-text' and text()='Send to Transfer List']]", "Send To Transfer List")
        },
        { ElementKeys.TRANSFER_LIST, ("//div[contains(@class, 'ut-tile-transfer-list')]", "Go To Transfer List") },
        {
            ElementKeys.CLEAR_SOLD_TRANSFERS,
            ("//button[contains(text(), 'Clear Sold')]", "Clear Sold Transfers Button")
        },
        {
            ElementKeys.LISTABLE_ITEMS,
            ("//li[contains(@class, 'listFUTItem') and not(contains(@class, 'has-auction-data')) or contains(@class, 'has-auction-data expired')]",
                "Listable Items")
        },
        { ElementKeys.COMPARE_PRICE, ("//button[.//span[contains(text(), 'Compare Price')]]", "Compare Price Button") },
        {
            ElementKeys.COMPARE_PRICE_ITEMS,
            ("//section[contains(@class, 'ut-navigation-container-view') and contains(@class, 'ui-layout-right')]//li[contains(@class, 'listFUTItem') and not(contains(@class, 'expired'))]",
                "Compare Price Items")
        },
        {
            ElementKeys.COMPARE_PRICE_BUY_NOW,
            ("//span[text()='Buy Now:']/following-sibling::span[contains(@class, 'currency-coins value')]",
                "Buy Now Price")
        },
        {
            ElementKeys.LIST_ITEM_PRE_PRICE,
            ("//button[.//span[contains(text(), 'Re-list Item') or contains(text(), 'List on Transfer Market')]]",
                "List Item Pre Price Button")
        },
        {
            ElementKeys.MIN_PRICE_LIST_ITEM,
            ("(//div[contains(@class, 'panelActions')]//input[contains(@class, 'ut-number-input-control')])[1]",
                "Min Price Input List Item")
        },
        {
            ElementKeys.MAX_PRICE_LIST_ITEM,
            ("(//div[contains(@class, 'panelActions')]//input[contains(@class, 'ut-number-input-control')])[2]",
                "Max Price Input List Item")
        },
        { ElementKeys.LIST_ITEM, ("//button[contains(text(), 'List for Transfer')]", "List For Transfer") },
        {
            ElementKeys.TRANSFER_MARKET,
            ("//div[contains(@class, 'tile') and contains(@class, 'col-1-1') and contains(@class, 'ut-tile-transfer-market')]",
                "Transfer Market")
        },
        {
            ElementKeys.CLUB_ITEMS_TRANSFER_MARKET,
            ("//button[contains(text(), 'Club Items')]", "Club Items Transfer Market")
        },
        {
            ElementKeys.PLAYER_ITEMS_TRANSFER_MARKET,
            ("//button[contains(text(), 'Players')]", "Players Transfer Market")
        },
        { ElementKeys.QUALITY_DROPDOWN, ("//span[text()='Quality']/ancestor::div[3]", "Quality Dropdown") },
        { ElementKeys.NATIONALITY_DROPDOWN, ("//span[text()='Country/Region']/ancestor::div[3]", "Nationality Dropdown") },
        { ElementKeys.QUALITY_DROPDOWN_SILVER, ("//li[text()='Silver']", "Silver") },
        {
            ElementKeys.CLUB_ITEMS_TYPE_DROPDOWN,
            ("(//div[contains(@class, 'ut-item-search-view')]/div[contains(@class, 'inline-list-select')])[5]",
                "Club Items Choice")
        },
        { ElementKeys.CLUB_ITEMS_TYPE_DROPDOWN_BADGES, ("//li[text()='Badge']", "Club Items Choice Badges") },
        { ElementKeys.CLUB_ITEMS_TYPE_DROPDOWN_KITS, ("//li[text()='Kits']", "Club Items Choice Kits") },
        { ElementKeys.FIND_ALL_PRICE_INPUTS, ("//input[contains(@class, 'ut-number-input-control')]", "Price Inputs") },
        { ElementKeys.SEARCH, ("//button[contains(text(), 'Search')]", "Search Button") },
        {
            ElementKeys.AUCTION_ITEMS,
            ("//li[contains(@class, 'listFUTItem') and contains(@class, 'has-auction-data') and (contains(@class, 'selected') or not(contains(@class, 'expired'))) and not(ancestor::div[contains(@class, 'ut-navigation-container-view') and contains(@class, 'ui-layout-right')])]",
                "Auction Items")
        },
        {
            ElementKeys.COMPARE_PRICE_BACK_BUTTON,
            ("//section[contains(@class, 'ui-layout-right')]//button[contains(@class, 'ut-navigation-button-control')]",
                "Compare Price Back")
        },
        { ElementKeys.RESET, ("//button[contains(text(), 'Reset')]", "Reset") },
        { ElementKeys.MAKE_BID, ("//button[contains(text(), 'Make Bid')]", "Make Bid") },
        { ElementKeys.NEXT, ("//button[contains(text(), 'Next')]", "Next") },
        {
            ElementKeys.TRANSFER_TARGETS_TOTAL,
            ("//div[contains(@class, 'ut-tile-transfer-targets')]//div[contains(@class, 'total-transfers-data')]/span[contains(@class, 'value')]",
                "Transfer Target Total")
        },
        {
            ElementKeys.RARITY_DROPDOWN,
            ("//span[text()='Rarity']/ancestor::div[3]", "Rarity Dropdown")
        },
        {
            ElementKeys.MANAGER_ITEMS_TRANSFER_MARKET,
            ("//button[contains(text(), 'Managers')]", "Manager Transfer Market")
        },
        { ElementKeys.CONTINUE, ("//button[contains(text(), 'Continue')]", "Continue") },
        {
            ElementKeys.COMPARE_PRICE_LIST,
            ("//section[contains(@class, 'ui-layout-right')]//div[contains(@class, 'paginated-item-list')]",
                "Compare Price List")
        },
        {
            ElementKeys.MAX_BID_PRICE_INPUT,
            ("(//div[contains(@class, 'search-prices')]//input[contains(@class, 'ut-number-input-control')])[2]",
                "Max Bid Price Input")
        },
        {
            ElementKeys.MIN_BUY_NOW_PRICE_INPUT,
            ("(//div[contains(@class, 'search-prices')]//input[contains(@class, 'ut-number-input-control')])[3]",
                "Min Buy Now Price Input")
        },
        {
            ElementKeys.MAX_BUY_NOW_PRICE_INPUT,
            ("(//div[contains(@class, 'search-prices')]//input[contains(@class, 'ut-number-input-control')])[4]",
                "Max Buy Now Price Input")
        },
        { ElementKeys.COIN_TOTAL, ("//div[contains(@class, 'view-navbar-currency-coins')]", "Coin Total Text") },
        { ElementKeys.ITEM_NAME, ("div.entityContainer > div.name", "Item Name") },
        { ElementKeys.ITEM_TIME_REMAINING, ("div.auction-state span.time", "Item Time Remaining") }
    };
}