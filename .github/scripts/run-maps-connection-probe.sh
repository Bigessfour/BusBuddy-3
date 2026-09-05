#!/usr/bin/env bash
# Live Google Maps Platform smoke (Address Validation + Routes + Places). Requires GOOGLE_MAPS_API_KEY.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"
dotnet run --project .github/scripts/MapsConnectionProbe/MapsConnectionProbe.csproj -c Release -p:EnableWindowsTargeting=true "$@"
