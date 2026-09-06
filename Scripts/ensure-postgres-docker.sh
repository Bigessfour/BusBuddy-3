#!/usr/bin/env bash
# Start BusBuddy Postgres (docker compose profile db) and wait until healthy.
# Called from ./run-wpf.sh on the Mac host before launching the Windows VM.

set -euo pipefail

ROOT="$(cd "$(dirname "${0}")/.." && pwd)"
cd "${ROOT}"

PFX="==>"

if ! command -v docker >/dev/null 2>&1; then
  echo "ERROR: docker not found. Install Docker Desktop on the Mac host." >&2
  exit 1
fi

if ! docker info >/dev/null 2>&1; then
  echo "ERROR: Docker daemon is not running. Start Docker Desktop, then retry." >&2
  exit 1
fi

echo "${PFX} Starting Postgres (docker compose --profile db up -d)..."
docker compose --profile db up -d

echo "${PFX} Waiting for Postgres to accept connections..."
for _ in $(seq 1 45); do
  if docker compose --profile db exec -T postgres pg_isready -U busbuddy >/dev/null 2>&1; then
    echo "${PFX} Postgres is ready on localhost:5432 (database busbuddy_test)."
    exit 0
  fi
  sleep 1
done

echo "ERROR: Postgres container started but did not become healthy in time." >&2
exit 1
