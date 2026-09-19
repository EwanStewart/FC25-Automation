#!/usr/bin/env python3
"""Print recent EA security code subjects from Gmail, newest first.

Reads the OAuth credentials the google-workspace MCP already holds, so the bot
needs no secret of its own. One JSON object per line: internalDate and subject.
The caller extracts the code and enforces its own freshness cutoff.

Usage:
    fetch_ea_code.py --after <epoch-seconds> [--account <email>]
"""
import json
import os
import sys
import urllib.parse
import urllib.request

HOME = os.path.expanduser("~")
CRED_DIR = f"{HOME}/.google_workspace_mcp/credentials"
DEFAULT_ACCOUNT = "ewanstewarts6@gmail.com"
QUERY = "in:anywhere subject:security"


def access_token(account):
    with open(f"{CRED_DIR}/{account}.json") as handle:
        stored = json.load(handle)
    body = urllib.parse.urlencode({
        "client_id": stored["client_id"],
        "client_secret": stored["client_secret"],
        "refresh_token": stored["refresh_token"],
        "grant_type": "refresh_token",
    }).encode()
    resp = json.load(urllib.request.urlopen(
        urllib.request.Request(stored["token_uri"], data=body)))
    return resp["access_token"]


def get(url, token):
    req = urllib.request.Request(url, headers={"Authorization": f"Bearer {token}"})
    return json.load(urllib.request.urlopen(req, timeout=20))


def messages(account, after):
    token = access_token(account)
    query = urllib.parse.quote(f"{QUERY} after:{after}")
    listing = get(
        "https://gmail.googleapis.com/gmail/v1/users/me/messages"
        f"?q={query}&maxResults=10", token)
    found = []
    for entry in listing.get("messages", []):
        detail = get(
            "https://gmail.googleapis.com/gmail/v1/users/me/messages/"
            f"{entry['id']}?format=metadata&metadataHeaders=Subject", token)
        headers = detail.get("payload", {}).get("headers", [])
        subject = next((h["value"] for h in headers if h["name"].lower() == "subject"), "")
        found.append({"internalDate": int(detail.get("internalDate", 0)), "subject": subject})
    found.sort(key=lambda item: item["internalDate"], reverse=True)
    return found


def main():
    args = sys.argv[1:]
    after = 0
    account = os.environ.get("FC_CODE_ACCOUNT", DEFAULT_ACCOUNT)
    while args:
        if args[0] == "--after":
            after = int(args[1])
            args = args[2:]
        elif args[0] == "--account":
            account = args[1]
            args = args[2:]
        else:
            args = args[1:]
    result = 0
    try:
        for item in messages(account, after):
            print(json.dumps(item))
    except Exception as error:
        sys.stderr.write(f"fetch_ea_code: {type(error).__name__}: {error}\n")
        result = 1
    sys.exit(result)


if __name__ == "__main__":
    main()
