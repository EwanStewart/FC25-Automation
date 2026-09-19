#!/usr/bin/env bash
set -euo pipefail

repoDir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
logDir="${FC25_LOG_DIR:-$HOME/dev/personal/fc25-logs}"
dll="$repoDir/Automation/bin/Release/net10.0/Automation.dll"

export DISPLAY="${DISPLAY:-:0}"
export XAUTHORITY="${XAUTHORITY:-/run/user/$(id -u)/gdm/Xauthority}"
export HOME="${HOME:-/home/$(id -un)}"
export PATH="/usr/local/bin:/usr/bin:/bin:$PATH"

mkdir -p "$logDir"
logFile="$logDir/sbc-$(date +%Y%m%d-%H%M%S).log"

{
  echo "SBC run started $(date -Is)"
  docker compose -f "$repoDir/docker-compose.yml" up -d --wait
  if [ ! -f "$dll" ]; then dotnet build "$repoDir/Automation/Automation.csproj" -c Release; fi
  dotnet "$dll" --capture-sbc --no-shutdown
  dotnet "$dll" --capture-club --no-shutdown
  dotnet "$dll" --summarise-sbc --no-shutdown
  echo "SBC run finished $(date -Is)"
} >> "$logFile" 2>&1
