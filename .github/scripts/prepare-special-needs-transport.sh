#!/usr/bin/env bash
# Apply BusBuddy migrations and seed special-needs transport test data.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

export BUSBUDDY_CONNECTION="${BUSBUDDY_CONNECTION:-Host=localhost;Port=5432;Database=busbuddy_test;Username=busbuddy;Password=busbuddy_dev;Include Error Detail=true}"

echo "==> Ensuring Postgres (docker compose --profile db)"
docker compose --profile db up -d
for _ in $(seq 1 30); do
  if pg_isready -h localhost -p 5432 -U busbuddy >/dev/null 2>&1; then
    break
  fi
  sleep 1
done

echo "==> Migrate + special-needs prep"
dotnet run --project BusBuddy.DbPrep/BusBuddy.DbPrep.csproj --configuration Release -- "$@"
