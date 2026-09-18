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

The bid ceiling is the resale estimate after the 5 percent tax, minus a margin, rounded down to the market's bid increment. The resale estimate is the median of the three lowest buy-now prices seen for that item in the last seven days. Compare Price only runs when the history is empty or older than six hours. Bids go only on auctions with 2 to 20 minutes left, and open bids are capped at half the coin balance. Every bid is recorded in the Bids table and marked won, lost or sold as the routine sees the outcome. Listings go up one increment under the lowest buy-now and are skipped when that would not cover what was paid. The knobs live in Automation/BiddingStrategy.cs.

Run the tests with `dotnet test`.

## Running on Windows

Build in your IDE, then run Start.bat.
