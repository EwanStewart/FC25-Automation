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

The ask-based estimate for an item is the second-lowest buy-now ask across every Compare Price page. Every ask counts, whatever its price, and two asks are enough. Estimates are kept for seven days and refreshed after six hours. An item already known to be too cheap to qualify is left alone for a day, then priced again, so a card whose market has moved cannot stay ruled out on a stale reading. The bot bids the minimum the auction allows, only when that minimum is at or below the break-even ceiling: the estimate after the 5 percent tax, minus a 700 coin profit margin, rounded down to the bid increment. It bids only on auctions with up to 20 minutes left, including their last minute, once per item per run, and keeps open bids under half the coin balance. On each results page the bot first hides the rows it will never bid on: its own bids, rows outside the time window, items already bid on this run, and items whose cached estimate is too cheap for the current bid. Only the remaining candidates are clicked. Every bid is recorded in the Bids table with its segment, the minimum bid, current bid, buy-now, minutes left and ask count, and marked won, lost or sold as the routine sees the outcome. Every Compare Price read is stored in CompareReads and every search pass in Passes.

### Sales feedback

Realised sales feed back into the estimate in two ways. Both live in Automation/SalesFeedback.cs.

- Per item: an item that sold in the last seven days is valued at its median sale price. A single sale is averaged with the ask-based estimate instead. A sale-based value never rises more than 1.5 times above the ask-based one. Without sales the ask-based floor stands.
- Per segment: the bot compares each sold price with the estimate it bid on. It takes the median ratio over the last 14 days and shrinks it towards 1.0 with five virtual sales. The result is clamped to 0.5 to 1.2 and scales every ask-based estimate in that segment. One sale therefore moves the ratio by a few percent at most.

### Segment demotion

A segment is badges, kits, or one snipe filter such as players:om-silvers. Before each pass the bot counts won and lost bids in the segment over the last seven days. Ten or more resolved bids with a win rate under 10 percent demote the segment. A demoted segment is skipped for six hours after its last bid. It is then probed with at most three bids so it can earn its way back. At maintenance time the bot marks expired and outbid rows on the Transfer Targets screen as lost. Open bids older than two hours are marked lost as a fallback.

### Player sniping

The filter ring in Automation/SnipeFilters.cs is empty, so no player pass runs and a cycle is badges and kits only. Add a filter to the ring to turn sniping back on, for example a SnipeFilter named Scotland cards with the nationality Scotland. The rest of this section describes the pass when a filter is configured.

The player snipe pass does not bid from the search results. It watches candidates and bids in the last minute of each auction from the Transfer Targets screen, where the web app updates every row in place.

1. Search players with the next filter in the snipe ring (RING in Automation/SnipeFilters.cs), max bid 500 and min buy now 1000. A filter has a name, a quality and any of nationality, league, club and position. The web app only enables the Club dropdown once a league is chosen, so a club filter carries its league. Each search is recorded under the filter's name and the ring resumes after the last recorded name, so the filters take turns across runs.
   The ring holds one filter as of 18 September 2026: Scotland cards, any quality. That week's Marquee Matchups squad Celtic v Rangers asks for one Scotland player at minimum Bronze quality with only 14 chemistry, so it is the cheapest of the four squads and Scottish fodder of every quality feeds it. Leaving the quality dropdown alone returns bronze, silver and gold Scots in one search, so a single pass sees the whole pool. Measured separately against minimum bids of 300 to 550, Scotland silvers ask 1,000 to 1,400 and 9 of 13 sampled cards clear a 700 coin margin; bronze Scots ask 1,000 to 1,100 against minimum bids of 150 to 300 and 3 of 5 clear it. England silvers, the previous filter, cleared 0 of 5 live with ask floors of 500 to 550. Note that the margin sets the bid ceiling, so cheap cards leave little room to win a contested auction: a 1,000 coin card at a 700 margin gives a 250 ceiling, two bid increments above a 150 start, while every card the bot has won or sold was estimated at 1,600 or more. Marquee Matchups changes every Thursday at 18:00 UK time and this set expires on 24 September 2026, so the filter needs revisiting each week.
2. Page through the results while rows are within 20 minutes of ending, including rows in their last minute. A scan stops early when Transfer Targets or the Transfer List is full or after two minutes. The same limits apply to the badge and kit passes. Price each candidate as usual and click Watch on every item whose minimum bid sits at or below the bid ceiling, until 15 items are watched. Only rows with two minutes or more left are watched (SNIPE_WATCH_MIN_MINUTES), because the scan can run two more minutes before the loop starts. A watch counts only when the server accepts it: the Unwatch button must be enabled after the click and the captured PUT watchlist must have returned 200; when the observer is running, no captured reply means no watch. A refused watch is retried once after a second and recorded as watch-refused with the status. The bot waits 1.5 s after each accepted watch before it clicks the next row, so the request is answered before the panel is torn down.
3. Open Transfer Targets and poll it every second. A watched item with under a minute left is bid at the minimum the auction allows. An item that flips to outbid is bid again at the new minimum, as long as that minimum still clears the margin. The ceiling is the estimate after tax minus the 700 coin margin, capped at 1500 coins. The loop ends when no watched item is live, after 25 minutes, or at the bid limit.
4. When the loop ends, the bot marks lost rows, sends won items to the transfer list and lists them at once, so a won player is on sale within a minute of the auction closing rather than at the next maintenance.
5. A bid counts only once the row shows our highest bid or the server's reply says so. A bid that does not register, including a 461 from a stale row, is logged and the list is refreshed. A bid found on a watched row without a record is recorded as ours.
6. The web app freezes an auction when one of its status refreshes fails: the row keeps counting down but its bid stops changing, and a bid at the stale minimum draws a 461. The snapshot reads each auction's updating flag and age, and a row counts as frozen only in its last 60 seconds, when its age passes twice the app's refresh interval for the tier it was last updated in (1 s under 30 s left, 5 s under 60 s). Slower tiers are ignored, because the app's first refresh after a fetch can take minutes there and flagged healthy rows. The updating flag alone is not used, because it is true for the whole round trip of every healthy refresh. A refresh never costs a bid: when any watched row is due to be bid or confirmed, the poll bids and leaves the refresh for a later poll. Only when nothing is due, and a live watched row is frozen or the observer saw a failed trade status, does the poll refresh instead. A row inside the bid window is never unwatched to dirty the cache. The refresh dirties the app's cache without a reload: it clicks Clear Expired if the button is showing, otherwise unwatches one live row whose minimum already exceeds its ceiling, then re-enters Transfer Targets. Only when neither is possible does it fall back to reloading the web app. Each refresh is recorded as a refresh event with detail dirty or reload, and refreshes are at least 10 s apart. Expired rows are left in place until the loop ends so one is usually available to clear.

Rows are read with one injected script per poll rather than one WebDriver call per field. The script returns every row's classes, name, rating, position, time text and prices as JSON, and on Transfer Targets it joins each row to the web app's own auction object, which carries exact seconds remaining and the server's bid state. The join is trusted only when the object's start price matches the row. With exact seconds the bot bids at 15 seconds left (SNIPE_AIM_SECONDS) and falls back to the under-a-minute text when the object is missing. The script only reads; it installs no hooks and makes no requests of its own.

A network observer attaches to the web app's tab over the DevTools protocol through its own WebSocket, enables the Network domain and keeps the last two hundred responses from EA's auction endpoints: searches, trade status, the watch list and bid replies. It only listens; the page cannot see it and no request is added. Compare Price takes its asks from the captured search response when the first ask matches the first price rendered in the list, otherwise it scrapes the list as before. A snipe bid is confirmed from the server's reply when the item's trade id is known, otherwise from the row. NETWORK_OBSERVER in BiddingStrategy.cs turns it off. Candidate rows on a results page are chosen from the snapshot and clicked by index after the key is checked again, so no row is hidden or altered.

Every step lands in the SnipeEvents table: search, watch, watch-refused, skip with its reason (margin, cap or budget), bid, rebid, outbid, overtaken, unregistered, refresh, unwatch, missing, confirmed, http for every non-200 auction response, and the pacing events backoff, pass-ended and run-stopped. The Bids row for an item keeps the latest amount and counts rebids. Each filter is its own segment, players: followed by the filter name in lower case with hyphens, so calibration and demotion are judged per filter.

### Pacing and backoff

The web app has no rate limiting of its own, and EA answers a burst of searches with 429, 512 and 401 replies and reports the session's request rate in its telemetry. The bot therefore paces itself. Page turns in every results scan are at least 2.5 s apart plus 0.5 to 1 s of random jitter. Compare Price reads are at least 3 s apart, read one page by default and a second only when the first returned fewer than two asks, and are skipped when the row's minimum bid already exceeds the segment's cap. Every transfermarket request the network observer captures counts against a search budget of 20 per rolling minute and 300 per rolling hour; when the budget is spent the pass waits for a slot rather than skipping the check. The hour count lives in the process, so it starts again with each run.

The observer's non-200 statuses drive the backoff. The first 429 or 512 in a pass pauses the bot for 60 s and doubles the pacing gaps for the rest of that pass. A second ends the pass. A 458, 426 or 494 stops the whole run, including the listing that follows the snipe loop, because carrying on turns a soft limit into a market lock. Each pass logs one pacing line with the pages turned, searches made, waits imposed and backoffs taken. The knobs live in BiddingStrategy.cs.

Listings undercut the market by one increment, with the start price one increment under that. If the start price would not cover what the item cost after tax, the listing goes up at break-even instead. The cost comes from the panel's "Bought For" figure or the recorded bid. The knobs, including the feedback, demotion and rotation thresholds, live in Automation/BiddingStrategy.cs.

Run the tests with `dotnet test`. Add `--loop 10` to keep cycling every ten minutes instead of shutting down after one pass, or `--no-shutdown` for a single pass that leaves the machine on. Add `--snipe` to skip the badge and kit passes and run only maintenance and the snipe pass; maintenance stays because it frees transfer target slots and lists what the last pass won.

For a scheduled bot, add `*/10 * * * * /path/to/FC25-Automation/cron.sh` to the crontab. cron.sh runs run.sh once every 70 minutes (FC25_INTERVAL_MINUTES to change), under a lock, and run.sh brings up the database, sets the display for Chrome and writes a log per run to ~/dev/personal/fc25-logs.

## Running on Windows

Build in your IDE, then run Start.bat.

## Player catalogue

The bot keeps a local copy of the EA Sports FC player catalogue in MySQL. The rest of the system can then turn an id into a name without asking anyone, which matters because EA's own club API returns owned items with no name on them, only assetId and resourceId.

MySQL/players.sql creates the two tables. docker-compose only seeds setup.sql, so apply the catalogue schema once by hand:

```
docker exec -i fc25-mysql mysql -uroot -proot < MySQL/players.sql
```

--import-players runs the import and exits. Program.Main does not call it yet, so add the call where options.ImportPlayers is read:

```
if (options.ImportPlayers) Environment.Exit(Catalogue.CatalogueProgram.Run());
```

### Sources

The default source is the EA web app itself, and it needs no key, no cookie and no token. Two plain GETs do the job. The first reads the web app page for window.fut_guid and window.fut_year. The second reads players.json under those two values. Never hardcode the guid: EA regenerates it on every content push, which is why the import resolves it each run.

That file holds two arrays, Players and LegendsPlayers, and the import reads both. A record is an asset id, a first and last name, an optional common name and a base rating. It carries no club, league, nation, rarity or position, so the import leaves those columns null rather than guessing them. Per-card attributes only exist in FUT-DB.

FUT-DB is the second source, documented at https://api.fut-db.com/api/doc/index.html, with its account portal at app.futdatabase.com. It wants a key in the X-AUTH-TOKEN header, which belongs in the .env file at the root of the repository under FUT_DB_KEY. A free key authenticates but carries no daily allowance at all: the reply is 429 with x-premium 0 and x-ratelimit-limit 0. Premium is 79 euros a month for 20,000 requests a day. Pick this source only when per-card attributes are needed; CatalogueSources.Create names both.

### Refreshing

The EA file is served with an ETag and a Last-Modified stamp, and the import stores the stamp on the import row. The next run sends it back as If-Modified-Since. EA's edge answers 304 to that, and the run ends having saved nothing and recorded the outcome as unchanged. Do not reach for the ETag: EA's edge returns a full 200 to If-None-Match even when the ETag matches exactly. A full import of 20,034 players takes about 20 seconds against a local MySQL. An unchanged refresh takes under a second.

### Schema

Players is keyed on the pair of source and source_id, so both sources can fill the table without fighting over one key. For the EA source, source_id is EA's asset id. For FUT-DB, it is FUT-DB's own row id, which is not an EA id at all.

The ids the rest of the system joins on live in their own columns, both BIGINT. asset_id is EA's asset id, which FUT-DB publishes as resourceBaseId. resource_id is EA's resource id, unique and null for every EA web app row. Join on those numbers, never on a name. Filter by source when both sources have run, because FUT-DB writes one row per card version and the EA file writes one row per player.

Each row also holds the full and common names, the rating, the preferred position, the alternate positions as one comma separated column, the club, league, nation and rarity ids, and a last_updated stamp. FUT-DB gives club, league, nation and rarity as numbers that line up with EA's own numbering: Manchester City is club 10, the Premier League is league 13 and Belgium is nation 7. Resolve a number to a name through /api/clubs, /api/leagues, /api/nations and /api/rarities. Indexes cover asset_id, rating, club, league, nation, position and name. An import rewrites a player in place, so a refresh leaves one row per card.

CatalogueImports records each run: its source, the number of items written, the last page finished, the page total, the content tag, the start and finish times and the outcome.

### Pacing and resuming

The importer waits PAGE_DELAY_MS between pages, 1.5 seconds by default, set in Automation/Catalogue/CatalogueProgram.cs. MAX_PAGES_PER_RUN caps a run and defaults to no cap. A run that stops part way leaves its import row open at the last page it finished, and the next run carries on from the page after it. A run that reaches the end closes its row, so the run after that starts at page one and refreshes the catalogue.

Swap in another provider by writing another IPlayerSource. The HTTP calls are the only part of Automation/Catalogue that touches the network, so the tests drive the parsing and mapping from saved pages under Automation.Tests/CatalogueFixtures.

## Squad building challenges

The SBC solver reads a challenge, drafts a squad from the club and offers it for approval in a local panel. It never bids, buys, lists or submits. Approving writes a row and stops there.

### Requirements

A challenge arrives from GET /ut/game/fc27/sbs/setId/{setId}/challenges. Its elgReq array groups by eligibilitySlot: one SCOPE entry sets the comparison, an optional PLAYER_COUNT turns the slot into a count of matching players, and a slot with no count constrains every player. The eligibility key numbers come from SBCEligibilityKey in the shipped FC 27 bundle, so SCOPE is 13, PLAYER_QUALITY is 3 and ALL_PLAYERS_CHEMISTRY_POINTS is 36.

RequirementParser reads that JSON. RequirementTextParser reads the same requirements as the panel renders them, lines such as "Scotland: Min. 1 Player" and "Player Quality: Exactly Silver", and produces the same model. Names resolve through a NameLookup because the catalogue holds no per-card attributes. A key or a line the parser does not understand becomes an Unsupported requirement, which always fails, so a squad never reads as valid while something went unread.

### Chemistry

ChemistryCalculator follows UTSquadChemCalculatorUtils.calculate from the FC 27 web app bundle. Eleven field slots, three points each, thirty-three in total. Contributions gather per club, league and nation, and only from a player standing in a position its card lists; a player out of position scores nothing and feeds no threshold. The manager feeds its league and nation but never its club. Icons and Heroes take the full three and lift every league in the squad once.

The threshold table is not shipped with the client. FC 27 fetches it from /ut/game/fc27/chemistry/profiles along with the club team links from /chemistry/teamlinks, and neither has been captured. The default table is the published one: club 2, 4 and 7 players for one point each, league 3, 5 and 8, nation 2, 5 and 8. Against the real active squad that scores 24 where the app reported 25. A single club team link between a women's club and its men's club closes the gap exactly, which matches what the squad holds, but it is unproven. Treat chemistry as an estimate until the two tables are captured.

### Solving

SquadSolver builds a CP-SAT model over the eleven slots. A candidate may only stand in a position it can play. Owned cards cost a hundredth of their market average, so the objective spends them freely and buys as little as it can. A gap is filled by a synthetic candidate carrying an attribute specification, a quality, a rating band and any pinned nation, league, club or rarity, because the market searches on attributes and the catalogue holds no per-card attributes. Specifications are generated per slot so two purchases never share an invented club.

The squad rating bound constrains the mean, which implies the real rating, so the solver is conservative and can miss a squad that only clears the bar on the above-average bonus. The real rating is always recomputed and reported. A star rating requirement or an unsupported requirement makes the solver refuse rather than draft something it cannot check.

### Wiring and running

Automation/Program.cs is untouched. Wire these entry points beside the existing catalogue and club switches:

    SbcProgram.ImportChallenges(path)   stores a captured challenges body
    SbcProgram.Draft(budget)            returns a drafted squad per stored challenge
    SbcProgram.Report(budget)           prints one

The panel is a separate project:

    dotnet run --project Automation.Web/Automation.Web.csproj

It binds to 127.0.0.1:5199 only and reads the same fc25 database. The list shows every stored challenge and whether it drafts. The challenge page shows the squad with names, ratings, positions and per slot chemistry, marks owned against bought, and shows every requirement with a pass or fail marker. Approve writes an SbcApprovals row and its slots, and the button stays disabled while any requirement fails.

MySQL/sbc.sql holds SbcChallenges, SbcApprovals and SbcApprovalSlots. It is mounted beside the other schema files in docker-compose.yml. An existing container has already run its init scripts, so apply it by hand with docker exec -i fc25-mysql mysql -uroot -proot < MySQL/sbc.sql.
