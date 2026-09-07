#!/usr/bin/env bash
# Read-only GCP / Maps Platform status for busbuddy-507301.
# Never prints API key strings. Does not enable, disable, or mutate resources.
set -euo pipefail

export PATH="/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin:${PATH:-}"
export CLOUDSDK_CORE_DISABLE_PROMPTS=1

PROJECT="${GCP_PROJECT:-${GOOGLE_CLOUD_PROJECT:-${CLOUDSDK_CORE_PROJECT:-busbuddy-507301}}}"
EXPECTED_APIS=(
  addressvalidation.googleapis.com
  places.googleapis.com
  routes.googleapis.com
)

if ! command -v gcloud >/dev/null 2>&1; then
  echo "gcloud not found. Install: brew install --cask google-cloud-sdk" >&2
  exit 1
fi

if ! gcloud auth list --filter=status:ACTIVE --format='value(account)' | grep -q .; then
  echo "No active gcloud account. Run: gcloud auth login" >&2
  exit 1
fi

echo "=== gcloud account / config ==="
echo "account: $(gcloud auth list --filter=status:ACTIVE --format='value(account)')"
echo "config project: $(gcloud config get-value project 2>/dev/null || true)"
echo "query project: ${PROJECT}"
echo

echo "=== project ==="
gcloud projects describe "${PROJECT}" \
  --format='yaml(projectId,name,projectNumber,lifecycleState)'
echo

echo "=== billing (enabled flag only) ==="
gcloud billing projects describe "${PROJECT}" --format='yaml(projectId,billingEnabled)'
echo

echo "=== Maps-related enabled APIs ==="
gcloud services list --enabled --project="${PROJECT}" \
  --filter='config.name~"maps" OR config.name~"places" OR config.name~"routes" OR config.name~"addressvalidation" OR config.name~"geocod"' \
  --format='table(config.name,config.title)'
echo

echo "=== expected Maps Platform APIs ==="
enabled="$(gcloud services list --enabled --project="${PROJECT}" --format='value(config.name)')"
for api in "${EXPECTED_APIS[@]}"; do
  if printf '%s\n' "${enabled}" | grep -qx "${api}"; then
    echo "OK    ${api}"
  else
    echo "MISS  ${api}"
  fi
done
echo

echo "=== API keys (metadata only; no keyString) ==="
gcloud services api-keys list --project="${PROJECT}" \
  --format='table(displayName,uid,createTime,restrictions.apiTargets[].service)'
echo

echo "Live Maps REST smoke (needs GOOGLE_MAPS_API_KEY, not gcloud):"
echo "  .github/scripts/run-maps-connection-probe.sh"
