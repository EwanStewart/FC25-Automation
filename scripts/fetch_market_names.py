#!/usr/bin/env python3
import argparse
import json
import re
import sys
import urllib.request

WEB_APP = "https://www.ea.com/ea-sports-fc/ultimate-team/web-app/"
LOC_PATH = "loc/en-US.json"
ITEMS_PATH = "fut/items/web"
NATION_KEY = "search.nationAbbr12.nation{id}"
LEAGUE_KEY = "global.leagueabbr15.{year}.league{id}"
LEAGUE_ABBR_KEY = "global.leagueabbr5.{year}.league{id}"
CLUB_KEY = "global.teamabbr15.{year}.team{id}"
CLUB_GROUPS = ("ClubItemTeams", "Teams")


def fetch(url):
    request = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
    with urllib.request.urlopen(request, timeout=30) as response:
        return response.read()


def fetch_json(url):
    return json.loads(fetch(url).decode("utf-8-sig"))


def content_version():
    html = fetch(WEB_APP).decode("utf-8", "replace")
    guid = re.search(r"fut_guid\s*=\s*[\"']([^\"']+)[\"']", html)
    year = re.search(r"fut_year\s*=\s*[\"']?(\d{4})[\"']?", html)
    if not guid or not year:
        raise SystemExit("The web app page carries no fut_guid or fut_year.")
    return guid.group(1), year.group(1)


def items(guid, year, name):
    return fetch_json(f"{WEB_APP}content/{guid}/{year}/{ITEMS_PATH}/{name}")


def nation_ids(guid, year):
    return sorted(int(value) for value in items(guid, year, "nations.json")["Nations"]["Nation"])


def league_ids(guid, year, clubs):
    listed = {int(entry["LeagueId"]) for entry in items(guid, year, "leagues.json")["Leagues"]["League"]}
    return sorted(listed | set(clubs.values()))


def club_leagues(guid, year):
    groups = items(guid, year, "teams.json")["Teams"]
    result = {}
    for group in CLUB_GROUPS:
        for entry in groups.get(group, []):
            result[int(entry["TeamId"])] = int(entry["LeagueId"])
    return result


def nations(loc, ids):
    return {str(value): loc[NATION_KEY.format(id=value)] for value in ids
            if NATION_KEY.format(id=value) in loc}


def leagues(loc, year, ids):
    result = {}
    for value in ids:
        name = loc.get(LEAGUE_KEY.format(year=year, id=value))
        abbreviation = loc.get(LEAGUE_ABBR_KEY.format(year=year, id=value))
        if name and abbreviation:
            result[str(value)] = {"name": name, "abbreviation": abbreviation}
    return result


def clubs(loc, year, ids):
    return {str(value): loc[CLUB_KEY.format(year=year, id=value)] for value in sorted(ids)
            if CLUB_KEY.format(year=year, id=value) in loc}


def build():
    guid, year = content_version()
    loc = fetch_json(f"{WEB_APP}{LOC_PATH}")
    pairs = club_leagues(guid, year)
    return {
        "year": year,
        "nations": nations(loc, nation_ids(guid, year)),
        "leagues": leagues(loc, year, league_ids(guid, year, pairs)),
        "clubs": clubs(loc, year, pairs),
    }


def main():
    parser = argparse.ArgumentParser(
        description="Rebuild MarketNamesData.json from the EA web app localisation file.")
    parser.add_argument("--out", required=True)
    options = parser.parse_args()
    data = build()
    with open(options.out, "w", encoding="utf-8") as handle:
        json.dump(data, handle, ensure_ascii=False, separators=(",", ":"))
        handle.write("\n")
    print(f"{len(data['nations'])} nations, {len(data['leagues'])} leagues, "
          f"{len(data['clubs'])} clubs for {data['year']}", file=sys.stderr)


if __name__ == "__main__":
    main()
