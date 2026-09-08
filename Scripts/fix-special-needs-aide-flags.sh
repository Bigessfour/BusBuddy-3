#!/usr/bin/env bash
#
# Enforce specs/students.md pickup rule 3 for a special-needs AM route:
#   "Special needs - home pickup on a designated special-needs route,
#    equipped bus, trained driver, aide."
# and the field-table default "RequiresAide ... Default true for special-needs runs."
#
# Sets RequiresSpecialNeedsBus = true and RequiresAide = true for every student
# whose Students."AMRoute" equals the supplied route name, and fills the legacy
# Students."SpecialNeeds" free-text column to match
# StudentSpecialNeedsHelper.SyncLegacySpecialNeedsText.
#
# Raw SQL on purpose: the EF model may expect columns a pending migration has not
# added yet, so any EF-based write to Students would fail. This touches only
# columns that already exist.
#
# The script contains no student data. Identify the cohort by route name only.
# It prints counts, never rows.
#
# Usage:
#   Scripts/fix-special-needs-aide-flags.sh "AM Special Needs Bus 5" [--dry-run]
#
# Environment:
#   PGDATABASE / PGUSER / PGHOST / PGPORT / PGPASSWORD   standard libpq vars
#   BUSBUDDY_PG_CONTAINER  when set, run psql inside this Docker container
#                          instead of using a local psql client
#
set -euo pipefail

ROUTE_NAME="${1:-}"
DRY_RUN="${2:-}"

if [[ -z "$ROUTE_NAME" ]]; then
    echo "usage: $0 <route-name> [--dry-run]" >&2
    exit 2
fi

PGDATABASE="${PGDATABASE:-busbuddy_test}"
PGUSER="${PGUSER:-busbuddy}"
PGPASSWORD="${PGPASSWORD:-busbuddy_dev}"
PGHOST="${PGHOST:-localhost}"
PGPORT="${PGPORT:-5432}"
export PGDATABASE PGUSER PGPASSWORD PGHOST PGPORT

run_psql() {
    if [[ -n "${BUSBUDDY_PG_CONTAINER:-}" ]]; then
        docker exec -e PGPASSWORD="$PGPASSWORD" -i "$BUSBUDDY_PG_CONTAINER" \
            psql -U "$PGUSER" -d "$PGDATABASE" -v ON_ERROR_STOP=1 "$@"
    else
        psql -v ON_ERROR_STOP=1 "$@"
    fi
}

STATEMENT_END="COMMIT;"
if [[ "$DRY_RUN" == "--dry-run" ]]; then
    STATEMENT_END="ROLLBACK;"
    echo "DRY RUN - changes will be rolled back"
fi

run_psql -v route="$ROUTE_NAME" <<SQL
BEGIN;

\echo '== cohort flag counts (before) =='
SELECT count(*)                                           AS students,
       count(*) FILTER (WHERE "RequiresSpecialNeedsBus")  AS req_sn_bus,
       count(*) FILTER (WHERE "RequiresAide")             AS req_aide,
       count(*) FILTER (WHERE "PickupStopId" IS NULL)     AS home_pickup,
       count(*) FILTER (WHERE "Active")                   AS active,
       count(*) FILTER (WHERE btrim("SpecialNeeds") <> '') AS legacy_text_set
FROM "Students"
WHERE "AMRoute" = :'route';

\echo '== set special-needs bus + aide (idempotent) =='
UPDATE "Students"
SET "RequiresSpecialNeedsBus" = true,
    "RequiresAide"            = true,
    "UpdatedDate"             = now(),
    "UpdatedBy"               = 'spec-fix-aide'
WHERE "AMRoute" = :'route'
  AND (NOT "RequiresSpecialNeedsBus" OR NOT "RequiresAide");

\echo '== sync legacy SpecialNeeds text (idempotent) =='
UPDATE "Students"
SET "SpecialNeeds" = 'Special needs bus required',
    "UpdatedDate"  = now(),
    "UpdatedBy"    = 'spec-fix-aide'
WHERE "AMRoute" = :'route'
  AND "RequiresSpecialNeedsBus"
  AND btrim("SpecialNeeds") = '';

\echo '== cohort flag counts (after) =='
SELECT count(*)                                           AS students,
       count(*) FILTER (WHERE "RequiresSpecialNeedsBus")  AS req_sn_bus,
       count(*) FILTER (WHERE "RequiresAide")             AS req_aide,
       count(*) FILTER (WHERE "PickupStopId" IS NULL)     AS home_pickup,
       count(*) FILTER (WHERE "Active")                   AS active,
       count(*) FILTER (WHERE btrim("SpecialNeeds") <> '') AS legacy_text_set
FROM "Students"
WHERE "AMRoute" = :'route';

$STATEMENT_END
SQL
