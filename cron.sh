#!/usr/bin/env bash
set -euo pipefail

repoDir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
intervalMinutes="${FC25_INTERVAL_MINUTES:-70}"
stateDir="${FC25_LOG_DIR:-$HOME/dev/personal/fc25-logs}"
stamp="$stateDir/last-run"
lock="$stateDir/run.lock"

mkdir -p "$stateDir"

due=1
if [ -f "$stamp" ]; then
  last=$(cat "$stamp")
  now=$(date +%s)
  if [ $(( now - last )) -lt $(( intervalMinutes * 60 )) ]; then due=0; fi
fi

if [ "$due" -eq 1 ]; then
  exec /usr/bin/flock -n -o "$lock" bash -c "date +%s > '$stamp' && '$repoDir/run.sh'"
fi
