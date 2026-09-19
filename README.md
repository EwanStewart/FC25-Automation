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

The ask-based estimate for an item is the second-lowest buy-now ask across every Compare Price page. Every ask counts, whatever its price, and two asks are enough. Taking the second rather than the first stops one rogue cheap listing setting the price of a card. A snipe filter can ask for the lowest instead, through ResaleBasis on the filter, and the Bundesliga managers filter does. Only that filter's own bidding changes: badge and kit ceilings and the price won cards are listed at still use the second-lowest ask. The filter's margin, resale basis and price age travel together as a PricePolicy, which also decides which rows a results page keeps, so a filter with a 300 coin margin no longer has its candidates pruned against the standard 700. Estimates are kept for seven days and refreshed after six hours. An item already known to be too cheap to qualify is left alone for a day, then priced again, so a card whose market has moved cannot stay ruled out on a stale reading. A snipe filter can shorten both of those through PriceAgeMinutes, which replaces the six hours and the day with one age. The Bundesliga managers filter sets ten minutes, so a manager is priced afresh on every pass rather than carrying a reading all day. The bot bids the minimum the auction allows, only when that minimum is at or below the break-even ceiling: the estimate after the 5 percent tax, minus a 700 coin profit margin, rounded down to the bid increment. It bids only on auctions with up to 20 minutes left, including their last minute, once per item per run, and keeps open bids under half the coin balance. On each results page the bot first hides the rows it will never bid on: its own bids, rows outside the time window, items already bid on this run, and items whose cached estimate is too cheap for the current bid. Only the remaining candidates are clicked. Every bid is recorded in the Bids table with its segment, the minimum bid, current bid, buy-now, minutes left and ask count, and marked won, lost or sold as the routine sees the outcome. Every Compare Price read is stored in CompareReads and every search pass in Passes.

### Sales feedback

Realised sales feed back into the estimate in two ways. Both live in Automation/SalesFeedback.cs.

- Per item: an item that sold in the last seven days is valued at its median sale price. A single sale is averaged with the ask-based estimate instead. A sale-based value never rises more than 1.5 times above the ask-based one. Without sales the ask-based floor stands.
- Per segment: the bot compares each sold price with the estimate it bid on. It takes the median ratio over the last 14 days and shrinks it towards 1.0 with five virtual sales. The result is clamped to 0.5 to 1.2 and scales every ask-based estimate in that segment. One sale therefore moves the ratio by a few percent at most.

### Segment demotion

A segment is badges, kits, or one snipe filter such as players:om-silvers. Before each pass the bot counts won and lost bids in the segment over the last seven days. Ten or more resolved bids with a win rate under 10 percent demote the segment. A demoted segment is skipped for six hours after its last bid. It is then probed with at most three bids so it can earn its way back. At maintenance time the bot marks expired and outbid rows on the Transfer Targets screen as lost. Open bids older than two hours are marked lost as a fallback.

### Sniping

The filter ring in Automation/SnipeFilters.cs drives the pass. An empty ring means no snipe pass runs and a cycle is badges and kits only.

The snipe pass does not bid from the search results. It watches candidates and bids in the last minute of each auction from the Transfer Targets screen, where the web app updates every row in place.

1. Search with the next filter in the snipe ring (RING in Automation/SnipeFilters.cs), max bid set by the filter and min buy now 1000. A filter has a name, a market, a margin, a bid ceiling, a resale basis, a price age, and any of quality, nationality, league, club and position. The market is Players or Managers and picks which button the search screen opens; managers offer only league and nation, so the other fields stay empty for them. The web app only enables the Club dropdown once a league is chosen, so a club filter carries its league. Each search is recorded under the filter's name and the ring resumes after the last recorded name, so the filters take turns across runs.
   The ring holds one filter as of 19 September 2026: Bundesliga managers, any nationality and any rarity. A manager carries a league and a nation and nothing else, so one league returns the whole German pool in a single search. The filter asks for a 300 coin margin and bids no more than 1000, well under the 700 and 1500 a player filter uses, because managers change hands for far less than fodder cards. The filter prices a manager on the cheapest listing of that same card, because a manager pool is thin enough that the one cheap seller is who we have to undercut. The pool is only about six distinct managers, so one pass prices all of them; a ten minute price age stops the next pass trading on an hour-old reading. Read the skip reasons in SnipeEvents after the first few passes: margin skips everywhere mean the pool is too cheap to clear 300 and the margin should come down, and wins that will not resell mean it should go up.
2. Page through the results while rows are within 20 minutes of ending, including rows in their last minute. A scan stops early when Transfer Targets or the Transfer List is full or after two minutes. The same limits apply to the badge and kit passes. Price each candidate as usual and click Watch on every item whose minimum bid sits at or below the bid ceiling, until 15 items are watched. Only rows with two minutes or more left are watched (SNIPE_WATCH_MIN_MINUTES), because the scan can run two more minutes before the loop starts. A watch counts only when the server accepts it: the Unwatch button must be enabled after the click and the captured PUT watchlist must have returned 200; when the observer is running, no captured reply means no watch. A refused watch is retried once after a second and recorded as watch-refused with the status. The bot waits 1.5 s after each accepted watch before it clicks the next row, so the request is answered before the panel is torn down.
3. Open Transfer Targets and poll it every second. A watched item with under a minute left is bid at the minimum the auction allows. An item that flips to outbid is bid again at the new minimum, as long as that minimum still clears the margin. The ceiling is the estimate after tax minus the filter's margin, capped at the filter's max bid. The loop ends when no watched item is live, after 25 minutes, or at the bid limit.
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

The SBC solver reads a challenge, drafts a squad from the club and offers it for approval in a local panel. Approving writes the approval and queues fulfilment. Fulfilment buys the missing cards and builds the squad, and stops there. Nothing in the code path can complete a challenge, and nothing sells, lists or discards a card.

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

### Fulfilment

Approving queues a fulfilment row and one gap row per slot the squad has to buy, inside the same web request, so the panel stays fast. Nothing is bought there. A separate run picks the queue up.

Two ceilings bound the spend. The run ceiling is the approved estimate plus fifty per cent, fixed when the approval is queued. It is held against the standing bids rather than a running count, so raising a bid on one gap costs only the difference and a resumed run inherits what the last one committed. The per-card ceiling is the solver's estimate for that gap plus a quarter, capped at a thousand coins of margin and floored at 250 coins, and it is trimmed to whatever the run ceiling still allows. A gap that cannot be afforded at the market floor aborts the whole run rather than letting a loop bid on.

The buyer walks the gaps in slot order. It searches the player market on quality, position and the per-card ceiling as the maximum bid, and pins the country, league and club dropdowns to whatever the gap names, so a narrow gap searches for the card it wants instead of paging through the whole market. The dropdowns take option text, so Automation/Sbc/MarketNamesData.json maps each id to the name EA's own client renders. It is built by scripts/fetch_market_names.py from the web app's localisation file and EA's nations.json, leagues.json and teams.json, and is refreshed by re-running that script against Automation/Sbc.

A club pin carries its league with it, because the club dropdown filters on the selected league and club names repeat across leagues. An id with no shipped name, or a shared club name with no league to narrow it, is left unpinned and the reason is written into the gap row. A pin the live dropdown does not offer leaves that dropdown broad rather than failing the run.

Every listing in the captured search response is still checked against the gap specification. A listing whose club, league, nation, rarity, rating or playable positions do not hold is rejected outright. The cheapest card that fits wins, ties going to whichever ends soonest. Bidding goes through the same typing, clicking and outcome cross-check the snipe pass uses, so there is one bidding implementation and it inherits the existing pacing and backoff.

Buying a gap is a snipe, not a purchase. The search shortlists the cards on the page that match the gap, sit under the card ceiling and end within three minutes, watches all of them, and then polls the transfer targets. A card is bid on only once it is inside its last fifteen seconds and is not already ours, one bid at a time, so the run never holds two live bids and cannot win two cards for one slot. Winning ends the gap: the card goes straight to the club and nothing else is bid on. Each poll sees only the trades that gap watched, so a card the trading pass won is never mistaken for this gap's own.

Every bid is written to the database before it is placed and its outcome after, so a crash cannot leave a spend untracked. A re-run reads the trades back first and turns each standing bid into won, outbid or expired. Won gaps are never bought again. Outbid and expired gaps are retried. A gap that is already won but whose card is still on the transfer targets is sent to the club at the start of the next run. A bid written down that cannot be read back on any trade stops the run and is recorded as unresolved, because a retry there could buy the same card twice. A bid the market refuses stops the run the same way.

Once every gap is won, the builder opens the challenge and places each card, slot by slot. Pressing Add Player on a slot opens a club search panel pinned to that slot, and the cards only appear once Search is pressed. The wanted row is found by matching the item id against the club search collection the app holds, never by position in a response. Each placement is verified against the squad entity the app itself holds, because the client serves a cached squad with no request and a placement checked against a captured response always looks missing. A slot that will not take its card stops the build with a record. Filling the last slot is the end of the job.

Fulfilment is dry by default and a dry run places no bid and moves no card. It searches, evaluates, writes down what it would buy and at what price, commits against the same ledger so it exercises the real ceiling, and leaves the approval queued so the live run still picks it up.

There are three modes. --place-live sits between the other two: it clicks cards into the squad for real while buying stays simulated, so placement can be exercised without a coin leaving the account. Its market agent is wrapped so that Bid throws rather than sends, and a card the run only pretended to buy is never placed. A run that did not buy live stays queued whatever it placed, because a squad completed with a simulated card is not finished.

    ./fulfil.sh                 dry run, takes the shared run.lock
    ./fulfil.sh --place-live    real placement, simulated buying
    ./fulfil.sh --fulfil-live   the same run, allowed to bid

Automation/Program.cs is untouched. The --fulfil-sbc flag falls through its existing default branch into the bot, which runs Fc25.FulfilSbcRoutine instead of the trading routine. Always pass --no-shutdown, as that default branch shuts the machine down without it. fulfil.sh does both.

MySQL/fulfilment.sql holds SbcFulfilments, SbcFulfilmentGaps and SbcFulfilmentPlacements, mounted beside the other schema files. Apply it to an existing container by hand with docker exec -i fc25-mysql mysql -uroot -proot < MySQL/fulfilment.sql. The unique keys on fulfilment and slot are what make a re-run idempotent.
