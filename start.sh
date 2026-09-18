#!/usr/bin/env bash
set -euo pipefail

repoDir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

docker compose -f "$repoDir/docker-compose.yml" up -d --wait
dotnet run --project "$repoDir/Automation/Automation.csproj" -c Release
