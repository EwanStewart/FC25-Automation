Selenium based C# application which automates the listing, pricing, and bidding of items in FC25 Web App.

MySQL backend to keep track of historical pricing.

## Running on Ubuntu

Requirements:

- .NET 10 SDK (`sudo apt install dotnet-sdk-10.0`)
- Google Chrome
- Docker with the compose plugin

Copy .env.example to .env and set FC_PASSWORD. The app types it on the EA sign-in page when that page appears.

Start the database and the app:

```bash
./start.sh
```

The script brings up MySQL 8.4 in a container named fc25-mysql, seeded from MySQL/setup.sql with root/root credentials, then runs the app with `dotnet run`. Selenium Manager downloads a chromedriver matching the installed Chrome on first run.

Two things to know before running:

- The app kills every running Chrome process on start and uses its own profile under `~/.local/share/SeleniumChromeProfile`.
- When the routine finishes it schedules a machine shutdown one minute later.

## Bidding rules

The ask-based estimate for an item is the second-lowest buy-now ask across every Compare Price page. Every ask counts, whatever its price, and two asks are enough. Estimates are kept for seven days and refreshed after six hours, except for items already known to be too cheap to qualify, which are skipped without another Compare Price. The bot bids the minimum the auction allows, only when that minimum is at or below the break-even ceiling: the estimate after the 5 percent tax, minus a 1000 coin profit margin, rounded down to the bid increment. It bids only on auctions with up to 20 minutes left, including their last minute, once per item per run, and keeps open bids under half the coin balance. On each results page the bot first hides the rows it will never bid on: its own bids, rows outside the time window, items already bid on this run, and items whose cached estimate is too cheap for the current bid. Only the remaining candidates are clicked. Every bid is recorded in the Bids table with its segment, the minimum bid, current bid, buy-now, minutes left and ask count, and marked won, lost or sold as the routine sees the outcome. Every Compare Price read is stored in CompareReads and every search pass in Passes.

### Sales feedback

Realised sales feed back into the estimate in two ways. Both live in Automation/SalesFeedback.cs.

- Per item: an item that sold in the last seven days is valued at its median sale price. A single sale is averaged with the ask-based estimate instead. A sale-based value never rises more than 1.5 times above the ask-based one. Without sales the ask-based floor stands.
- Per segment: the bot compares each sold price with the estimate it bid on. It takes the median ratio over the last 14 days and shrinks it towards 1.0 with five virtual sales. The result is clamped to 0.5 to 1.2 and scales every ask-based estimate in that segment. One sale therefore moves the ratio by a few percent at most.

### Segment demotion

A segment is badges, kits, or players:snipe. Before each pass the bot counts won and lost bids in the segment over the last seven days. Ten or more resolved bids with a win rate under 10 percent demote the segment. A demoted segment is skipped for six hours after its last bid. It is then probed with at most three bids so it can earn its way back. At maintenance time the bot marks expired and outbid rows on the Transfer Targets screen as lost. Open bids older than two hours are marked lost as a fallback.

### Silver player sniping

The silver player pass no longer bids from the search results. It watches candidates and bids in the last minute of each auction from the Transfer Targets screen, where the web app updates every row in place.

1. Search Silver players of one nation with max bid 500 and min buy now 1000. Only England is searched for now (SNIPE_NATIONS in NationRotation.cs); add nations there to rotate through them round-robin.
2. Page through the results while rows are within 20 minutes of ending, including rows in their last minute. A scan stops early when Transfer Targets or the Transfer List is full or after two minutes. The same limits apply to the badge and kit passes. Price each candidate as usual and click Watch on every item whose minimum bid sits at or below the bid ceiling, until 15 items are watched. A watch counts only when the server accepts it: the Unwatch button must be enabled after the click and any captured PUT watchlist must have returned 200. A refused watch is retried once after a second and recorded as watch-refused with the status. The bot waits 1.5 s after each accepted watch before it clicks the next row, so the request is answered before the panel is torn down.
3. Open Transfer Targets and poll it every second. A watched item with under a minute left is bid at the minimum the auction allows. An item that flips to outbid is bid again at the new minimum, as long as that minimum still clears the margin. The ceiling is the estimate after tax minus the 1000 coin margin, capped at 1500 coins. The loop ends when no watched item is live, after 25 minutes, or at the bid limit.
4. When the loop ends, the bot marks lost rows, sends won items to the transfer list and lists them at once, so a won player is on sale within a minute of the auction closing rather than at the next maintenance.
5. A bid counts only once the row shows our highest bid or the server's reply says so. A bid that does not register, including a 461 from a stale row, is logged and the list is refreshed. A bid found on a watched row without a record is recorded as ours.
6. The web app freezes an auction when one of its status refreshes fails: the row keeps counting down but its bid stops changing, and a bid at the stale minimum draws a 461. The snapshot reads each auction's updating flag and age, and a row counts as frozen when its age passes twice the app's refresh interval for the tier it was last updated in (1 s under 30 s left, 5 s under 60 s, 120 s under 600 s). The updating flag alone is not used, because it is true for the whole round trip of every healthy refresh. When any live watched row is frozen, or the observer saw a failed trade status since the last refresh, the poll refreshes the list instead of bidding. The refresh dirties the app's cache without a reload: it clicks Clear Expired if the button is showing, otherwise unwatches one live row whose minimum already exceeds its ceiling, then re-enters Transfer Targets. Only when neither is possible does it fall back to reloading the web app. Each refresh is recorded as a refresh event with detail dirty or reload, and refreshes are at least 10 s apart. Expired rows are left in place until the loop ends so one is usually available to clear.

Rows are read with one injected script per poll rather than one WebDriver call per field. The script returns every row's classes, name, rating, position, time text and prices as JSON, and on Transfer Targets it joins each row to the web app's own auction object, which carries exact seconds remaining and the server's bid state. The join is trusted only when the object's start price matches the row. With exact seconds the bot bids at 15 seconds left (SNIPE_AIM_SECONDS) and falls back to the under-a-minute text when the object is missing. The script only reads; it installs no hooks and makes no requests of its own.

A network observer attaches to the web app's tab over the DevTools protocol through its own WebSocket, enables the Network domain and keeps the last two hundred responses from EA's auction endpoints: searches, trade status, the watch list and bid replies. It only listens; the page cannot see it and no request is added. Compare Price takes its asks from the captured search response when the first ask matches the first price rendered in the list, otherwise it scrapes the list as before. A snipe bid is confirmed from the server's reply when the item's trade id is known, otherwise from the row. NETWORK_OBSERVER in BiddingStrategy.cs turns it off. Candidate rows on a results page are chosen from the snapshot and clicked by index after the key is checked again, so no row is hidden or altered.

Every step lands in the SnipeEvents table: search, watch, watch-refused, skip with its reason (margin, cap or budget), bid, rebid, outbid, overtaken, unregistered, refresh, unwatch, missing, confirmed, http for every non-200 auction response, and the pacing events backoff, pass-ended and run-stopped. The Bids row for an item keeps the latest amount and counts rebids. The pass is recorded under the players:snipe segment with the snipe role.

### Pacing and backoff

The web app has no rate limiting of its own, and EA answers a burst of searches with 429, 512 and 401 replies and reports the session's request rate in its telemetry. The bot therefore paces itself. Page turns in every results scan are at least 2.5 s apart plus 0.5 to 1 s of random jitter. Compare Price reads are at least 3 s apart, read one page by default and a second only when the first returned fewer than two asks, and are skipped when the row's minimum bid already exceeds the segment's cap. Every transfermarket request the network observer captures counts against a search budget of 20 per rolling minute and 300 per rolling hour; when the budget is spent the pass waits for a slot rather than skipping the check. The hour count lives in the process, so it starts again with each run.

The observer's non-200 statuses drive the backoff. The first 429 or 512 in a pass pauses the bot for 60 s and doubles the pacing gaps for the rest of that pass. A second ends the pass. A 458, 426 or 494 stops the whole run, including the listing that follows the snipe loop, because carrying on turns a soft limit into a market lock. Each pass logs one pacing line with the pages turned, searches made, waits imposed and backoffs taken. The knobs live in BiddingStrategy.cs.

Listings undercut the market by one increment, with the start price one increment under that. If the start price would not cover what the item cost after tax, the listing goes up at break-even instead. The cost comes from the panel's "Bought For" figure or the recorded bid. The knobs, including the feedback, demotion and rotation thresholds, live in Automation/BiddingStrategy.cs.

Run the tests with `dotnet test`. Add `--loop 10` to keep cycling every ten minutes instead of shutting down after one pass, or `--no-shutdown` for a single pass that leaves the machine on.

For a scheduled bot, add `*/10 * * * * /path/to/FC25-Automation/cron.sh` to the crontab. cron.sh runs run.sh once every 70 minutes (FC25_INTERVAL_MINUTES to change), under a lock, and run.sh brings up the database, sets the display for Chrome and writes a log per run to ~/dev/personal/fc25-logs.

## Running on Windows

Build in your IDE, then run Start.bat.
