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

The resale estimate for an item is the second-lowest buy-now ask across every Compare Price page, and it needs at least three asks to count. Estimates are kept for seven days and refreshed after six hours, except for items already known to be too cheap to qualify, which are skipped without another Compare Price. The bot bids the minimum the auction allows, only when that minimum is at or below the break-even ceiling: the estimate after the 5 percent tax, minus a 1000 coin profit margin, rounded down to the bid increment. It bids only on auctions with 3 to 20 minutes left, once per item per run, and keeps open bids under half the coin balance. Every bid is recorded in the Bids table with the minimum bid, current bid, buy-now, minutes left and ask count, and marked won, lost or sold as the routine sees the outcome. Every Compare Price read is stored in CompareReads.

Listings undercut the market by one increment, with the start price one increment under that. If the start price would not cover what the item cost after tax, the listing goes up at break-even instead. The cost comes from the panel's "Bought For" figure or the recorded bid. The knobs live in Automation/BiddingStrategy.cs.

Run the tests with `dotnet test`.

## Running on Windows

Build in your IDE, then run Start.bat.
