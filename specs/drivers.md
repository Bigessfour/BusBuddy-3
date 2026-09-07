# Drivers

## Purpose

A driver is the licensed person who operates a district bus on a published route or on a trip. BusBuddy-3 records who they are, whether they are legal and trained to drive, which route they normally run, and that they complete daily vehicle duties. This file is not a HR system spec.

BusBuddy-3 is a Syncfusion WPF .NET 9 desktop app on Windows. It is not hosted on AWS. Do not add a cloud LMS or CDL registry integration unless a later spec says so.

## Invariants

- MUST keep three things separate: the person, the credentials/training, and the assignment (home route vs trip vs substitute).
- MUST require a CDL Class A or B with school-bus (and bus) endorsement before the driver can be assigned to a student-carrying route or trip.
- MUST store training as dated records the Student Transportation Supervisor can produce for inspection — not a single “trained” checkbox.
- MUST treat summer-initiated training as valid for that school year by default, with explicit expiration/renewal dates.
- MUST assign a home route as the default for the year without making that assignment unchangeable.
- MUST allow substitute drivers and mid-year reassignment. “Permanent” means default, not immutable.
- MUST NOT overwrite the home-route assignment when the same driver is loaned to a trip. See `specs/trips.md`.
- MUST attribute pre-trip, post-trip, defect reports, and fueling to the driver **on duty that day** and to the bus they inspected — not as eternal properties of the person.
- MUST NOT invent a parallel driver model. Extend existing Core driver types/services.
- Default: one home route for the school year; training done before opening day.
- Exception: illness, spare driver, trip after the AM run, mid-year route change, expired endorsement.

## Relationships

- Driver `1` → `0..1` home Route (default AM and/or PM — do not assume both are the same record).
- Driver `1` → `0..*` trip assignments (temporary; see trips).
- Driver `1` → `0..*` credential / training documents.
- Driver on a date → `0..1` Bus inspected that session.
- Student Transportation Supervisor is the custodian of training records and the authority who assigns routes and trips.
- Aide/assistant for special-needs runs is a related role, not a second CDL driver record unless that person also drives.

## Data the app must store

| Field                | Type         | Required      | Notes                                                      |
| -------------------- | ------------ | ------------- | ---------------------------------------------------------- |
| DriverId             | existing key | yes           | Reuse Core identity.                                       |
| DisplayName          | string       | yes           | Operational PII. Do not dump rosters to git.               |
| Active               | bool         | yes           | Soft-retire. Do not delete if routes/trips reference them. |
| CdlClass             | enum: A, B   | yes to assign | Minimum Class A or B.                                      |
| SchoolBusEndorsement | bool         | yes to assign | Required for student routes.                               |
| BusEndorsement       | bool         | recommended   | Per state CDL package.                                     |
| CdlExpiration        | date         | yes to assign | Block new assignments after expiry; warn before.           |
| HomeRouteId          | route id     | no            | Default year assignment.                                   |
| IsSubstitutePool     | bool         | yes           | True if they cover others’ routes.                         |
| CanDriveSpecialNeeds | bool         | yes           | Extra training beyond base school-bus.                     |
| Notes                | string       | no            | Radio call sign, restrictions.                             |

### Training records (child collection)

| Field              | Type    | Required    | Notes                                                        |
| ------------------ | ------- | ----------- | ------------------------------------------------------------ |
| TrainingId         | key     | yes         | One row per course/event.                                    |
| Title              | string  | yes         | State-required course name.                                  |
| AgencyOrCurriculum | string  | yes         | Colorado / district program.                                 |
| CompletedOn        | date    | yes         | Usually summer before the year.                              |
| ValidThrough       | date    | yes         | Often end of school year.                                    |
| DocumentRef        | path/id | recommended | Proof of completion for inspection. Supervisor is custodian. |

Do not store scanned certificates in the git repo.

### Duty / inspection events (child collection, belong to the day + bus)

| Field     | Type                                  | Required | Notes                                            |
| --------- | ------------------------------------- | -------- | ------------------------------------------------ |
| EventId   | key                                   | yes      | Not a driver column.                             |
| DriverId  | fk                                    | yes      | Who performed it.                                |
| BusId     | fk                                    | yes      | Which vehicle.                                   |
| Session   | enum: AM, PM, Trip                    | yes      |                                                  |
| EventType | enum: PreTrip, PostTrip, Defect, Fuel | yes      |                                                  |
| EventedAt | datetime                              | yes      |                                                  |
| Notes     | string                                | no       | Defect description goes here and to maintenance. |

## Behaviors / UI

- Supervisor adds a driver, records CDL + endorsements + expiration, then attaches training rows.
- A driver with expired CDL or missing school-bus endorsement cannot be newly assigned to a student route or trip. Existing home assignment should surface a warning.
- Home-route picker sets the default published run. Changing it is a deliberate year or mid-year action.
- Trip assignment uses the same driver id and must warn if the trip overlaps the home route session.
- Daily pre/post-trip and fueling are logged against bus + driver + session. Incomplete pre-trip should be visible to the supervisor; this spec does not require blocking the route in software yet.
- Defect report notifies the supervisor / maintenance workflow. It does not by itself retire the bus.
- Substitute: supervisor assigns a pool driver to a route session for a date range. Home driver assignment remains unless they change it.

## Roles the spec assumes

- **Student Transportation Supervisor** — training custodian, route/trip assignment authority, recipient of defect and coverage issues.
- **Driver** — operates the assigned bus, inspects it, fuels as needed, reports defects, runs the published route or the assigned trip.
- **Aide** — required on special-needs runs; not a driver unless separately credentialed.

## Out of scope

- Full HR payroll, I-9, or medical-examiner card vault beyond a date field if Core already has one.
- Electronic ELD / live GPS of the driver.
- Automatic pull of CDL status from the state.
- Parent-facing driver profiles.

## Code anchors

| Spec term                | Existing code                                             |
| ------------------------ | --------------------------------------------------------- |
| Driver record            | existing Core driver model/service — extend, do not clone |
| Home route               | `BusBuddy.Core.Models.Route`                              |
| Bus inspected / fueled   | `IBusService`, Core bus model                             |
| Trip loan                | trip aggregate in `specs/trips.md`                        |
| Special-needs capability | used with `specs/students.md` and `specs/buses.md`        |
| AutoMapper (not maps)    | `MappingService`                                          |

## Worked examples

- Year default: driver completes summer training, CDL current, assigned to AM/PM Route 5 special needs for 2026-2027. That is the home route.
- Trip after AM: same driver is assigned to a 2:30 PM volleyball trip. Home PM route must be covered by a substitute or the trip is refused. HomeRouteId does not change.
- Substitute Thursday: pool driver runs Route 2 AM. Inspections that morning are logged under the substitute + that bus.
- Expired endorsement in March: warn on the driver record; do not silently keep dispatching new trips to them.

## Agent instructions

When changing driver code, read this file plus `specs/buses.md`, `specs/routes.md`, and `specs/trips.md`. Quote the invariant you implemented. Do not collapse credentials, home assignment, and same-day duty logs into one boolean-heavy driver row.
