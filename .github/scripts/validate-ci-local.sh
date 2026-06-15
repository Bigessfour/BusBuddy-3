#!/usr/bin/env bash
# Local pre-push validation mirroring CI gates.
# Docker: Linux Core build + Postgres (always runs).
# Host tests: Windows only (WPF / Microsoft.WindowsDesktop.App); macOS/Linux skip with notice.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "${ROOT}"

SOLUTION="BusBuddy.sln"
CONFIG="Release"
TEST_FILTER='Category!=Integration&Category!=InMemoryFlaky&(FullyQualifiedName~Core|Seed|Student|Route|Maintenance|PdfReport|Fleet|Gaps|ModelValidation)'

echo "==> Docker: build Core image + Postgres health"
docker compose --profile db --profile test build busbuddy-test
docker compose --profile db up -d postgres
trap 'docker compose --profile db down' EXIT

for _ in $(seq 1 30); do
  if docker compose exec -T postgres pg_isready -U busbuddy >/dev/null 2>&1; then
    break
  fi
  sleep 1
done
docker compose exec -T postgres pg_isready -U busbuddy

echo "==> Docker: verify Core build inside container"
docker compose --profile db --profile test run --rm busbuddy-test \
  dotnet build BusBuddy.Core/BusBuddy.Core.csproj \
  -c "${CONFIG}" --no-restore -p:EnableWindowsTargeting=true -v minimal

echo "==> Host: restore and build (compile gate)"
dotnet restore "${SOLUTION}" -p:EnableWindowsTargeting=true --verbosity minimal
dotnet build "${SOLUTION}" --configuration "${CONFIG}" --no-restore \
  -p:EnableWindowsTargeting=true /p:TreatWarningsAsErrors=false /p:WarningLevel=1

OS_NAME="$(uname -s)"
if [[ "${OS_NAME}" == "MINGW"* ]] || [[ "${OS_NAME}" == "MSYS"* ]] || [[ "${OS_NAME}" == "CYGWIN"* ]] || [[ "${OS_NAME}" == "Windows_NT" ]]; then
  echo "==> Host: run tests (Windows — same filter as CI)"
  dotnet test "${SOLUTION}" --configuration "${CONFIG}" --no-build \
    --verbosity normal \
    --filter "${TEST_FILTER}"
else
  echo "==> Skipping host dotnet test on ${OS_NAME}: WPF tests require Windows (CI runs on windows-latest)."
  echo "    Docker Core + host compile validation passed."
fi

echo "==> Local CI validation passed"
