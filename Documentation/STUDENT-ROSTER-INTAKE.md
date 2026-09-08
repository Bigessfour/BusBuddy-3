# Student roster intake (clerk workflow)

How a paper or Excel roster becomes student records in the local BusBuddy database.

> **This file is git-tracked. Every example below is a placeholder.**
> Never paste a real child's name, address, guardian, or phone number into this document,
> into a test, into `SeedDataService.cs`, or into a commit message.

Contract: [specs/students.md](../specs/students.md) —
_"MUST NOT commit student PII (name, address, guardian, medical notes, roster spreadsheets) to
GitHub. Load via clerk forms. Paper/Excel rosters stay in local clerk workflow only."_

## 1. Where local rosters live

```text
artifacts/rosters/<ROUTE>-<SESSION>-<SCHOOL-YEAR>.csv
artifacts/rosters/<ROUTE>-<SESSION>-<SCHOOL-YEAR>.notes.md
```

`artifacts/` is ignored by `.gitignore:259` and `*.csv` is ignored again by `.gitignore:1144`,
so a roster placed here is invisible to `git status` and cannot be staged by accident. That double
coverage is deliberate — losing one rule still leaves the other.

The `.notes.md` companion holds what the paper sheet carries but a `Student` row cannot: route
staffing (driver, aides), the full stop and time sequence including non-student waypoints, per-stop
driving directions, and a list of any cells that were hard to read. Stop order and times belong to
the route, not to the child ([specs/routes.md](../specs/routes.md)).

**Before you finish any roster work, prove nothing leaked:**

```bash
git status --porcelain                 # no roster file may appear here
git check-ignore -v artifacts/rosters/<file>   # must print a matching .gitignore rule
```

If a roster shows up as untracked-but-visible, stop and fix the ignore rule. Do not commit "just
this once."

## 2. CSV format

One header row of named columns, then one row per **child**. A three-sibling household is three
rows that repeat the same address and guardian — never one row with three names.

| Column                    | Maps to                    | Notes                                                             |
| ------------------------- | -------------------------- | ----------------------------------------------------------------- |
| `StopNumber`              | —                          | Kept for the clerk; not read by the importer.                     |
| `PickupTime`              | —                          | Kept for the clerk; stop times belong to the route.               |
| `StudentName`             | `StudentName`              | **Required.** Presence of this column selects the roster format.  |
| `Grade`                   | `Grade`                    | Blank is fine.                                                    |
| `School`                  | `School` + `DestinationId` | Must match a `Destinations.Name` exactly — see step 3.            |
| `SchoolYear`              | `SchoolYear`               | e.g. `2026-2027`. Blank falls back to the current year.           |
| `HomeAddress`             | `HomeAddress`              | Street line as printed.                                           |
| `City` / `State` / `Zip`  | `City` / `State` / `Zip`   | Needed for Address Validation. Leave blank if the sheet omits it. |
| `GuardianName`            | `ParentGuardian`           | Also used to group siblings into a `Family`.                      |
| `GuardianPhone`           | `CellPhone`                |                                                                   |
| `HomePhone`               | `HomePhone`                |                                                                   |
| `EmergencyContactName`    | `EmergencyContactName`     | A _second_ contact, distinct from the guardian.                   |
| `EmergencyContactPhone`   | `EmergencyPhone`           |                                                                   |
| `PickupMode`              | `PickupStopId`             | `Home` or `CatalogStop`. See the pickup rules below.              |
| `PickupStopId`            | `PickupStopId`             | Required when `PickupMode` is `CatalogStop`.                      |
| `AMRoute` / `PMRoute`     | `AMRoute` / `PMRoute`      | Route **name**. Blank means not assigned from this sheet.         |
| `RidesAm` / `RidesPm`     | `RidesAm` / `RidesPm`      | Optional. Omit and the assigned route states it — see below.      |
| `RequiresSpecialNeedsBus` | `RequiresSpecialNeedsBus`  | `true`/`false`/`yes`/`no`/`1`/`0`.                                |
| `RequiresAide`            | `RequiresAide`             | Default `true` on a special-needs run.                            |
| `RequiresWheelchair`      | `RequiresWheelchair`       |                                                                   |
| `RequiresSeatBelt`        | `RequiresSeatBelt`         |                                                                   |
| `HasMedicalNeeds`         | `HasMedicalNeeds`          |                                                                   |
| `Active`                  | `Active`                   | Defaults to `true`.                                               |
| `TransportationNotes`     | `TransportationNotes`      | Quote the field if it contains commas.                            |

Placeholder example — copy the shape, never the values:

```csv
StopNumber,PickupTime,StudentName,Grade,School,SchoolYear,HomeAddress,City,State,Zip,GuardianName,GuardianPhone,EmergencyContactName,EmergencyContactPhone,PickupMode,AMRoute,PMRoute,RequiresSpecialNeedsBus,RequiresAide,RequiresWheelchair,RequiresSeatBelt,HasMedicalNeeds,Active,TransportationNotes
1,7:00,TEST_STUDENT_01,3,Example School,2026-2027,100 Test St,TESTVILLE,CO,00000,TEST_GUARDIAN_01,555-0100,,,Home,TEST_AM_ROUTE,,true,true,false,false,false,true,"Side gate, no turnaround"
2,7:05,TEST_STUDENT_02,1,Example School,2026-2027,100 Test St,TESTVILLE,CO,00000,TEST_GUARDIAN_01,555-0100,,,Home,TEST_AM_ROUTE,,true,true,false,false,false,true,
```

### How AM/PM eligibility is decided

`specs/students.md` requires AM and PM eligibility to be settable "both, or neither, **independently**",
so the importer never assumes a run a child does not ride:

1. A `RidesAm` / `RidesPm` column, when present, is the answer. Use it when a child is eligible for a
   run that has no route assigned yet — eligibility is not assignment.
2. Otherwise the assigned route states it: a non-blank `AMRoute` means the child rides the AM run.
   This is the same rule the database backfill used.
3. Silence on both is _neither_, never "both". An AM-only sheet with a blank `PMRoute` column
   therefore imports as `RidesAm=true, RidesPm=false`.

The blank-form default in the clerk UI is both runs ticked, because most children ride both — but
that default lives on the form, where a human sees the checkboxes, not on the shared model.

### Route names are reconciled against the Routes table

`StudentService.ValidateStudentAsync` matches `Routes.RouteName` **exactly**, so a student assigned
to a route with no row cannot be saved or even edited. On import:

- A route name differing from an existing route only by word order is rewritten to the existing
  spelling, so the database stays the source of truth for canonical names and no roster file has to
  be edited. `AM Bus 5 Special Needs` resolves to `AM Special Needs Bus 5`.
- A name with no route at all gets an active route row created for it, flagged special-needs and
  sessioned by `RouteSession.Infer` when the name says "special needs".

To repair rows that were loaded before this existed, call
`ISeedDataService.EnsureRoutesForStudentAssignmentsAsync()`; it is also run at the end of `SeedAllAsync`.

### Things the importer will not do for you

- **No coordinates.** `Latitude` and `Longitude` are never read from CSV. They come from Google
  Address Validation, because a hand-typed coordinate is not a validated one
  ([specs/locations.md](../specs/locations.md)). Imported rows start unplottable by design — no
  0,0 pins, no US-centroid guesses.
- **Rows are skipped, loudly, when they contradict the domain contract.** Each skip is a Serilog
  warning naming the row number:
    - `RequiresSpecialNeedsBus=true` together with `PickupMode=CatalogStop` — special needs is home
      pickup on a special-needs route (`specs/students.md` pickup rule 3).
    - `PickupMode=CatalogStop` with no `PickupStopId` — a catalog stop must point at a published stop.
    - A blank `StudentName`.
- **Children already in the database are skipped by name.** Re-running an import is safe and adds
  nothing; it is not an update mechanism. Edit existing children on the clerk form.
- **Unrecognised header columns are ignored with a warning**, so an extra bookkeeping column will
  not fail the whole file.

### Legacy format still works

The older family-export shape (a group row, then a header row with `Fname, Lname, Grade, Address`)
is still accepted unchanged. It has no campus, route, or special-needs columns, which is why the
roster format above exists. A header matching neither shape throws.

## 3. Match campus names to the Destinations catalog first

`School` is linked to `Destinations` by **exact name match**. Paper sheets abbreviate ("LHS", "LMS"),
the catalog usually spells names out. A value that does not match imports the student with no
`DestinationId`, which leaves the record incomplete and unroutable.

List the catalog before you import:

```bash
docker compose up -d postgres
docker exec busbuddy-3-postgres-1 psql -U busbuddy -d busbuddy_test \
  -c 'SELECT "Name" FROM "Destinations" WHERE "DestinationType" = '"'"'School'"'"' ORDER BY 1;'
```

Then either write the catalog name in the CSV's `School` column, or add the campus to
`Destinations` first. Record the abbreviation-to-catalog mapping in the roster's `.notes.md` so the
paper sheet stays readable next to the import file.

The import prints `Unlinked=N` in its Serilog output; `N` should be `0`.

## 4. Run the import

### Option A — in the app (normal clerk path)

Students view → **Import CSV**, or the student form's **Import CSV** button. Both call
`ISeedDataService.ImportStudentsFromCsvAsync`. The view refreshes and reports how many were added.
This is the supported path and it runs in the Windows VM where the WPF app lives.

### Option B — command line

```bash
docker compose up -d postgres
dotnet run --project BusBuddy.DbPrep -c Release -p:EnableWindowsTargeting=true \
  -- import-roster artifacts/rosters/<ROUTE>-<SESSION>-<SCHOOL-YEAR>.csv
```

`import-roster` deliberately **does not apply migrations**, so importing a roster can never race a
pending schema change. If the schema is behind the model you will see
`column "..." of relation "Students" does not exist` — run `-- migrate` first, or wait for whoever
owns the pending migration.

After migrate, create any missing `Routes` rows so assigned students pass `ValidateStudentAsync`:

```bash
dotnet run --project BusBuddy.DbPrep -c Release -p:EnableWindowsTargeting=true -- ensure-routes
```

**On macOS**, `PostgresConnectionResolver` prefers `keys/mac-host-ip.txt`, which `run-wpf.sh` writes
with the Mac's UTM bridge address for the Windows VM. That address is unreachable from the Mac itself
when the VM is down, so a Mac-side CLI run needs a localhost override next to the binary:

```bash
OUT=BusBuddy.DbPrep/bin/Release/net9.0-windows
mkdir -p "$OUT/keys" && printf '127.0.0.1' > "$OUT/keys/mac-host-ip.txt"
```

That path is under `bin/` and therefore gitignored. Delete it before running the WPF app from the VM.

## 5. Verify the row count

`import-roster` prints a summary with **counts and flags only** — never names, addresses, or phone
numbers. Keep it that way in any log, screenshot, or issue you share:

```text
=== Roster import verification (PII-free) ===
Students in database:              9
Special-needs riders:             9
Aide required:                    9
Home pickup (no catalog stop):    9
Awaiting geocode:                 9
Campus not linked to Destinations: 0
```

Check that the added count equals the number of **children** on the sheet, not the number of printed
rows — a sibling row holds several children, and school drop-off rows hold none.

A useful independent cross-check on a route sheet that lists drop-offs: the per-campus counts in the
sheet's school column should equal the number of students you assigned to each campus. If those two
numbers disagree, the transcription is wrong somewhere.

Ad-hoc verification, still PII-free:

```bash
docker exec busbuddy-3-postgres-1 psql -U busbuddy -d busbuddy_test -c \
'SELECT count(*) AS students,
        count(DISTINCT "FamilyId") AS households,
        count(DISTINCT "School") AS campuses,
        sum(("RequiresSpecialNeedsBus")::int) AS special_needs,
        sum(("PickupStopId" IS NULL)::int) AS home_pickup,
        sum(("Latitude" IS NULL)::int) AS awaiting_geocode
 FROM "Students";'
```

## 6. Finish the records

Import is intake, not completion. After a successful import:

1. Fill in any missing `City` / `State` / `Zip` on the clerk form.
2. Run Address Validation so `Latitude` / `Longitude` and `PlaceId` are written by the geocoder.
   Until then the students are correctly excluded from the district map.
3. Confirm the special-needs route exists as a `Route` with `IsSpecialNeedsRoute = true`, an
   equipped bus, a trained driver, and an aide. Students carry the route **name** as text; the
   route row itself is route-domain work.
4. Add real secondary emergency contacts from the emergency cards. A paper route sheet usually has
   only one contact per child, and it is normally the guardian.
5. Review wheelchair, seat-belt, and medical flags against IEP and health records. A sheet that says
   nothing about them is not evidence that nothing is needed.

## 7. Retention

The roster under `artifacts/rosters/` is a working copy. The paper sheet remains the record of
authority. When the school year rolls over, start a new file rather than editing last year's, so the
transcription history stays intact locally.
