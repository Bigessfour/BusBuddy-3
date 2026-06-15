#!/usr/bin/env bash
# One-time (or idempotent) repo setup for solo-dev CI/CD governance.
# Requires: gh CLI authenticated with admin on Bigessfour/BusBuddy-3
set -euo pipefail

REPO="${GITHUB_REPOSITORY:-Bigessfour/BusBuddy-3}"

echo "Enabling auto-merge and delete-branch-on-merge for ${REPO}..."
gh api "repos/${REPO}" -X PATCH \
  -f allow_auto_merge=true \
  -f delete_branch_on_merge=true

echo "Creating or updating master branch ruleset..."
RULESET_JSON="$(cat <<'EOF'
{
  "name": "Master solo-dev gates",
  "target": "branch",
  "enforcement": "active",
  "conditions": {
    "ref_name": {
      "include": ["refs/heads/master"],
      "exclude": []
    }
  },
  "rules": [
    {
      "type": "pull_request",
      "parameters": {
        "required_approving_review_count": 0,
        "dismiss_stale_reviews_on_push": false,
        "require_code_owner_review": false,
        "require_last_push_approval": false,
        "required_review_thread_resolution": false
      }
    },
    {
      "type": "required_status_checks",
      "parameters": {
        "strict_required_status_checks_policy": true,
        "required_status_checks": [
          {"context": "Build & Test"},
          {"context": "Security (CodeQL)"}
        ]
      }
    },
    {
      "type": "deletion"
    },
    {
      "type": "non_fast_forward"
    }
  ]
}
EOF
)"

EXISTING_ID="$(gh api "repos/${REPO}/rulesets" --jq '.[] | select(.name=="Master solo-dev gates") | .id' 2>/dev/null | head -1 || true)"

if [[ -n "${EXISTING_ID}" ]]; then
  gh api "repos/${REPO}/rulesets/${EXISTING_ID}" -X PUT --input - <<< "${RULESET_JSON}"
  echo "Updated ruleset id ${EXISTING_ID}"
else
  gh api "repos/${REPO}/rulesets" -X POST --input - <<< "${RULESET_JSON}"
  echo "Created ruleset"
fi

echo "Enabling Dependabot security updates..."
gh api "repos/${REPO}/vulnerability-alerts" -X PUT 2>/dev/null || true

echo "Done. Required merge gates: Build & Test, Security (CodeQL). PR review count: 0."
