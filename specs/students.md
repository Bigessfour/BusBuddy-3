# Students

## Purpose

Students are why BusBuddy-3 exists. A student is a child enrolled for school transportation from a pickup place to an assigned school (and, when needed, between schools). This file defines meaning and invariants for the WPF clerk app. It is not a UI mockup.

BusBuddy-3 is a Syncfusion WPF .NET 9 desktop app that runs on a Windows machine. It is not hosted on AWS. Do not add cloud hosting, parent portals, or live fleet tracking to satisfy this spec.

## Invariants

- MUST store one student record per child, not one record per bus ride.
- MUST assign every transported student to a school destination (`IDestinationService` / school location).
- MUST record how the student is picked up: `Home` or `CatalogStop`.
- MUST allow AM eligibility, PM eligibility, both, or neither, independently.
- MUST treat special-needs status as a first-class flag that forces home pickup on a special-needs route, with a trained driver and aide on a specially equipped bus.
- MUST treat a transfer (school A → school B during the day) as a second assignment on a transfer route, not a second student.
- MUST validate home and stop addresses with Google Address Validation and persist lat/lng from geocoding. Clerks do not type coordinates as the source of truth.
- MUST NOT commit student PII (name, address, guardian, medical notes, roster spreadsheets) to GitHub. Load via clerk forms. Paper/Excel rosters stay in local clerk workflow only.
- MAY delete a student when the clerk chooses a required reason: **Mistake** (row entered in error), **Moved** (left the district), or **Not attending** (otherwise no longer at this school). The app MUST ask for that reason before the row is removed and MUST write an operational deletion log (student id, student number, reason, optional brief note, related-assignment counts). Do not put names, addresses, or guardian data in that log.
- MUST NOT delete a student by accident. There is no default reason; the clerk must pick one and confirm. Archive (`Active=false`) remains the path when the child may return and the record should stay on the roster.
- MUST NOT invent a parallel student model. Extend `IStudentService` / existing Core student types.
- Default: assignment is stable for the school year.
- Exception: mid-year move, school change, eligibility change, or same-day “not riding” (that last one is a rider exception on the route, not a new student type).

## Relationships

- Student `1` → `1` assigned school (destination location).
- Student `1` → `0..1` home location (required when pickup mode is `Home`).
- Student `1` → `0..1` catalog pickup stop (required when pickup mode is `CatalogStop`).
- Student `1` → `0..*` assignments (AM route, PM route, transfer route).
- Assignment → Route → default Bus + default Driver.
- Special-needs student → special-needs route + equipped bus + driver + aide.
- Guardian contacts belong to the student; they are operational, not decorative.

## Data the app must store

| Field                | Type                    | Required           | Notes                                                                         |
| -------------------- | ----------------------- | ------------------ | ----------------------------------------------------------------------------- |
| StudentId            | existing key            | yes                | Use current Core identity; do not create a second key.                        |
| DisplayName          | string                  | yes                | PII. Clerk-only. Never commit.                                                |
| SchoolYear           | string/year             | yes                | e.g. 2026-2027.                                                               |
| AssignedSchoolId     | location/school id      | yes                | Destination for daily delivery.                                               |
| PickupMode           | enum: Home, CatalogStop | yes                | Rural default is often Home; in-town default is often CatalogStop.            |
| HomeAddress          | string                  | when Home          | Validate with Google Address Validation.                                      |
| HomeLat, HomeLng     | decimal                 | after validate     | Written by geocoder, not by hand.                                             |
| CatalogStopId        | pickup-stop id          | when CatalogStop   | From `IPickupStopService`.                                                    |
| RidesAm              | bool                    | yes                | Independent of PM.                                                            |
| RidesPm              | bool                    | yes                | Independent of AM.                                                            |
| IsSpecialNeeds       | bool                    | yes                | Home pickup + special route/bus/aide.                                         |
| RequiresAide         | bool                    | when special needs | Default true for special-needs runs.                                          |
| TransferRequired     | bool                    | yes                | In-day school-to-school.                                                      |
| TransferRouteId      | route id                | when transfer      | Separate from AM/PM home-to-school routes.                                    |
| Active               | bool                    | yes                | False archives a student who may return. Delete is a separate, logged action. |
| GuardianName / Phone | string                  | recommended        | PII. For clerk/dispatcher contact.                                            |
| Notes                | string                  | no                 | Medical/operational. PII. Do not put in git.                                  |

Same-day absence, sports opt-out, or “not riding this afternoon” are **not** columns on the student. They are rider exceptions on that date’s route/trip.

## Behaviors / UI

- Clerk adds or edits a student on a form. Saving an address runs Address Validation + geocoding before the record is complete.
- After a home validates, **hint** the nearest published catalog stop inside the walk radius (`StopSuggestMaxMeters`). Do not auto-assign it. The clerk selects the stop if that corner is a safe pickup.
- If no catalog stop is in range, pickup mode stays **Home**. If other Home students with validated coordinates sit in the same radius, **hint** the clerk to publish a catalog stop and then assign those riders — do not create the stop automatically. Generate already shares one waypoint per catalog stop; home riders stay on the published list until the clerk attaches them.
- Map plots a student only after lat/lng exist. Unvalidated addresses show as incomplete, not as pins at 0,0 or the US centroid.
- District map center for clerks is Lamar/Wiley CO (~38.0872, -102.6208) until a route or student extent is chosen.
- AM-only and PM-only students appear on the matching session roster and map only.
- Special-needs students appear on the special-needs route (example: AM Bus #5), not on a general in-town catalog-stop route, unless a clerk explicitly reassigns them.
- Transfer assignments show as a separate in-day movement, not as a duplicate child.
- Parent/guardian notification of pickup place and time comes from the **published route**, not from live GPS (live GPS is out of scope).
- Clerk deletes a student only after choosing Mistake, Moved, or Not attending (optional brief note). The route row stays. The student's assignment, exception rows, and a home stop that names only that student are removed with the record and counted in the deletion log. A shared stop keeps the other riders.

## Out of scope

- Live bus GPS / parent “where is the bus” tracking.
- Parent mobile self-service.
- Automatic routing of a new student onto a route without a clerk assignment.
- Committing transcribed paper rosters to the git repo.
- AWS, Amplify, or any hosted backend.

## Code anchors

| Spec term                           | Existing code                                                              |
| ----------------------------------- | -------------------------------------------------------------------------- |
| Student persistence / forms         | `IStudentService` and Core student model                                   |
| Logged deletion                     | `IStudentService.DeleteStudentAsync` + `StudentDeletionLog`                |
| Assigned school                     | `IDestinationService`                                                      |
| In-town gathering point             | `IPickupStopService`                                                       |
| Map plot of homes / schools / stops | `IGeoDataService`, `IMapsGeoService`, `MapViewModel`                       |
| Address → coordinates               | Google Address Validation + Geocoding                                      |
| Object mapping (DTO ↔ VM)           | `MappingService` (AutoMapper — not maps)                                   |
| Worked roster (local only)          | `artifacts/rosters/AM-Bus-5-Special-Needs-2026-2027.*` — do not commit PII |

## Pickup rules (do not collapse these)

1. **Rural home pickup** — bus goes to the validated home. Common for out-of-town and required for special needs.
2. **In-town catalog stop** — student walks a reasonable distance to a published safe stop shared with neighbors. The stop is the route waypoint, not each house.
3. **Special needs** — home pickup on a designated special-needs route, equipped bus, trained driver, aide.
4. **Transfer** — additional in-day assignment between two schools or program sites.
5. **AM/PM split** — sports, clubs, or family schedules can produce AM-only or PM-only riders without changing pickup mode.

## Mid-year change

When a student moves or changes school **inside the district**:

1. Keep the same StudentId.
2. Enter the new address or school.
3. Validate + geocode.
4. Clerk reassigns pickup mode, stop, and AM/PM routes.
5. Old assignment is ended with an effective date. Do not silently rewrite history.

When a student **leaves** (moved out of district, no longer attending, or the row was a data-entry mistake):

1. Prefer **Archive** if the child may return later this year.
2. **Delete** only after the clerk picks Mistake, Moved, or Not attending and confirms. That writes the deletion log and removes the roster row.

## Worked examples (Wiley / Lamar area)

- Special-needs AM home rider: active, `IsSpecialNeeds=true`, `PickupMode=Home`, `RidesAm=true`, `RidesPm=false`, assigned to the special-needs AM route (Bus #5 pattern). Map pin is the home, not a town corner stop.
- In-town general rider: `PickupMode=CatalogStop`, both AM and PM, pin is the catalog stop. Home may still be stored for contact and eligibility, but it is not a route waypoint.
- Transfer rider: same student keeps AM/PM home-to-school assignments and adds `TransferRequired=true` with a transfer route between two school destinations.

## Agent instructions

When changing student code, read this file plus `specs/locations.md`, `specs/routes.md`, and `specs/maps.md`. Quote the invariant you implemented. If code and this spec conflict, stop and say so instead of inventing a third model.
