#!/usr/bin/env bash
set -euo pipefail

repoDir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
logDir="${FC25_LOG_DIR:-$HOME/dev/personal/fc25-logs}"
lock="$logDir/run.lock"
dll="$repoDir/Automation/bin/Release/net10.0/Automation.dll"

export DISPLAY="${DISPLAY:-:0}"
export XAUTHORITY="${XAUTHORITY:-/run/user/$(id -u)/gdm/Xauthority}"
export HOME="${HOME:-/home/$(id -un)}"
export PATH="/usr/local/bin:/usr/bin:/bin:$PATH"

mkdir -p "$logDir"
logFile="$logDir/fulfil-$(date +%Y%m%d-%H%M%S).log"

run() {
  echo "Fulfilment run started $(date -Is)"
  docker compose -f "$repoDir/docker-compose.yml" up -d --wait
  if [ ! -f "$dll" ]; then dotnet build "$repoDir/Automation/Automation.csproj" -c Release; fi
  dotnet "$dll" --fulfil-sbc --no-shutdown "$@"
  echo "Fulfilment run finished $(date -Is)"
}

exec /usr/bin/flock -n -o "$lock" bash -c "$(declare -f run); run $* >> '$logFile' 2>&1"
