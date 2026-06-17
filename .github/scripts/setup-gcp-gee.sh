#!/usr/bin/env bash
# Bootstrap Google Cloud CLI + BusBuddy GEE credentials.
# Run after: brew install --cask google-cloud-sdk
set -euo pipefail

export PATH="/opt/homebrew/share/google-cloud-sdk/bin:/opt/homebrew/bin:$PATH"

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "${ROOT}"

# Override via env: GCP_PROJECT=ee-bigessfour ./setup-gcp-gee.sh
# Note: busbuddy-465000 in old configs was invalid. Use ee-bigessfour (Earth Engine default project).
PROJECT_ID="${GCP_PROJECT:-ee-bigessfour}"
SA_NAME="${GEE_SA_NAME:-bus-buddy-gee}"
SA_EMAIL="${SA_NAME}@${PROJECT_ID}.iam.gserviceaccount.com"
KEY_DIR="${ROOT}/keys"
KEY_FILE="${KEY_DIR}/bus-buddy-gee-key.json"

echo "==> Google Cloud SDK: $(gcloud version 2>/dev/null | head -1 || echo 'NOT INSTALLED')"
echo "==> Target project: ${PROJECT_ID}"
echo "==> Service account: ${SA_EMAIL}"
echo "==> Key output: ${KEY_FILE} (gitignored)"

if ! gcloud auth list --filter=status:ACTIVE --format='value(account)' 2>/dev/null | grep -q .; then
  echo ""
  echo "No active gcloud account. Run interactive login (browser will open):"
  gcloud auth login
  gcloud auth application-default login
fi

ACTIVE_ACCOUNT="$(gcloud auth list --filter=status:ACTIVE --format='value(account)' | head -1)"
echo "==> Active account: ${ACTIVE_ACCOUNT}"

gcloud config set project "${PROJECT_ID}"
gcloud config set account "${ACTIVE_ACCOUNT}"

echo "==> Enabling APIs (Earth Engine + IAM + Drive for export workflow)"
gcloud services enable \
  earthengine.googleapis.com \
  iam.googleapis.com \
  iamcredentials.googleapis.com \
  drive.googleapis.com \
  --project="${PROJECT_ID}" 2>/dev/null || echo "(Some APIs may already be enabled or need billing)"

echo "==> Listing service accounts"
if gcloud iam service-accounts describe "${SA_EMAIL}" --project="${PROJECT_ID}" >/dev/null 2>&1; then
  echo "Service account exists: ${SA_EMAIL}"
else
  echo "Creating service account ${SA_NAME}..."
  gcloud iam service-accounts create "${SA_NAME}" \
    --display-name="BusBuddy Google Earth Engine" \
    --project="${PROJECT_ID}"
fi

echo "==> Granting Earth Engine access (project roles)"
for ROLE in roles/earthengine.writer roles/earthengine.viewer; do
  gcloud projects add-iam-policy-binding "${PROJECT_ID}" \
    --member="serviceAccount:${SA_EMAIL}" \
    --role="${ROLE}" \
    --condition=None \
    --quiet 2>/dev/null || true
done

mkdir -p "${KEY_DIR}"
if [[ -f "${KEY_FILE}" ]]; then
  echo "Key file already exists at ${KEY_FILE} — skipping create (delete to regenerate)."
else
  echo "==> Creating service account key..."
  gcloud iam service-accounts keys create "${KEY_FILE}" \
    --iam-account="${SA_EMAIL}" \
    --project="${PROJECT_ID}"
  chmod 600 "${KEY_FILE}"
fi

echo "==> Updating appsettings GoogleEarthEngine section"
python3 - <<PY
import json, pathlib
root = pathlib.Path("${ROOT}")
patch = {
    "ProjectId": "${PROJECT_ID}",
    "ServiceAccountEmail": "${SA_EMAIL}",
    "ServiceAccountKeyPath": "keys/bus-buddy-gee-key.json",
}
for rel in [
    "appsettings.json",
    "appsettings.azure.json",
    "BusBuddy.WPF/appsettings.json",
    "BusBuddy.Core/appsettings.json",
]:
    p = root / rel
    if not p.exists():
        continue
    data = json.loads(p.read_text())
    gee = data.setdefault("GoogleEarthEngine", {})
    gee.update(patch)
    p.write_text(json.dumps(data, indent=2) + "\n")
    print(f"  updated {rel}")
PY

echo ""
echo "==> Fetch access token probe (service account)"
export GOOGLE_APPLICATION_CREDENTIALS="${KEY_FILE}"
TOKEN="$(gcloud auth application-default print-access-token 2>/dev/null || true)"
if [[ -n "${TOKEN}" ]]; then
  echo "Application default token acquired (length ${#TOKEN})"
else
  gcloud auth activate-service-account --key-file="${KEY_FILE}" --project="${PROJECT_ID}"
  TOKEN="$(gcloud auth print-access-token)"
  echo "Service account token acquired (length ${#TOKEN})"
fi

HTTP_CODE="$(curl -s -o /dev/null -w '%{http_code}' \
  -H "Authorization: Bearer ${TOKEN}" \
  "https://earthengine.googleapis.com/v1/projects/${PROJECT_ID}")"
echo "GEE API probe: HTTP ${HTTP_CODE} for projects/${PROJECT_ID}"

echo "Done. Next:"
echo "  1. Store in macOS Passwords: .github/scripts/store-gcp-passwords.sh"
echo "  2. Run: .github/scripts/GeeConnectionProbe (on Windows VM) or set GEE_ACCESS_TOKEN"
echo "  3. App startup loads Passwords -> env -> GcpCredentialBootstrap (Production-ready)"
echo "  4. Register Earth Engine for service account at https://signup.earthengine.google.com/ if probe fails"
