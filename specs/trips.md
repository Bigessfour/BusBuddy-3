# Trips

## Purpose

A trip is a one-time movement of a group (team, club, class, activity) from an origin location to a destination location. It is not a daily published route. Routes are year-stable and predictable. Trips are fluid: times slip, buses swap, and cancellations happen the same day.

The current office trip board is the clerk spreadsheet **Activity Schedule - Trip Schedule**. One spreadsheet row is one Trip. BusBuddy-3 is a Syncfusion WPF .NET 9 desktop app on Windows. It is not hosted on AWS. Do not build a hosted parent trip app or live dispatch feed to satisfy this spec.

## Invariants

- MUST treat Route and Trip as different aggregates. Never add `IsTrip` to `Route`. Never put these rows in the published daily route editor. A trip MUST NOT overwrite a driver’s or bus’s home-route assignment.
- MUST allow saving a row without a destination. Playoff rows with a ticket and PAX but no place/time MUST stay `MissingInfo`. Do not delete them.
- MUST upsert imports by `ExternalTicketNo` (board **Ticket #**). Do not create students. Do not commit manifests or PII to git.
- MUST map board columns as specified below. Do not auto-apply notes such as “adjust leave time to 1:30” until a clerk changes `PickupTime`.
- MUST require a validated destination, times, driver, and bus before status `Confirmed`.
- MUST NOT assign a bus the fleet header marks Out of Service without a hard warning.
- MUST treat “All buses and SPED” as multi-asset (`IsMultiAsset`). Do not smash that into one `BusId`.
- MUST link Day 1 / Day 2 or “both teams ride together” with `LinkedTripId`. Do not merge those rows into a Route.
- MUST plot a selected trip on the map only when coordinates exist. No 0,0 or US-centroid fallback pins.
- MUST print a trip ticket from Core `TripEvent` (not `Activity` / `Schedule`). Paper blanks for beginning/ending/total mileage, fuel entered, time departed, and time arrived back at the barn are **not** persisted columns.
- MUST persist clerk-edited trip purposes and sports/reasons in user settings (`ITripReasonCatalog`). Do not require a code change to add Track, Band, or a field-trip purpose.
- MUST add 1.5 hours (`TripEvent.PrePostTripInspectionHours`) to billed driver hours for every trip (`DriverHours` = wheel time + 1.5). Do not auto-insert PreTrip/PostTrip duty events. Do not change `HomeRouteId`.
- MUST NOT invent a second calendar or clone Route. Extend Core `TripEvent` if it exists.
- MUST NOT treat `MappingService.cs` as a map service. Live GPS stays off.
- Default: commuting distance inside normal operations (another school, field, event in the region).
- Exception: same-day cancel, delayed departure, bus swap, substitute driver, destination change, or overnight pending (flag + notes only — not a hotel planner).

## Office board → Trip fields

| Board column  | Trip field                                      | Notes                                                                                   |
| ------------- | ----------------------------------------------- | --------------------------------------------------------------------------------------- |
| Date          | `TripDate`                                      | Use the month-label year (e.g. Sep 2026).                                               |
| School        | `RequestingSchool`                              | HS, MS, AV, WA.                                                                         |
| Team / School | `GroupOrActivity`                               | Named group. Not a student roster.                                                      |
| Location      | `DestinationName`, then `DestinationLocationId` | After Google Address Validation.                                                        |
| Departs       | `PickupTime`                                    | May be empty (`MissingInfo`).                                                           |
| Returns       | `ReturnTime`                                    | May be next morning.                                                                    |
| Ticket #      | `ExternalTicketNo`                              | Upsert key on import.                                                                   |
| PAX           | `PlannedHeadcount`                              |                                                                                         |
| Rode          | `ActualHeadcount`                               | Separate; may be empty.                                                                 |
| Miles         | `PlannedMiles`                                  | Refresh `PathMiles` from Google Routes only after origin and destination are validated. |
| Driver        | `AssignedDriverId`                              | Resolve names.                                                                          |
| Bus #         | `AssignedBusId`                                 | Transit, Van, EXP, BJT, 55–57 are real assets.                                          |
| Notes         | `Notes`                                         | Keep clerk text; do not mutate times from notes.                                        |

Origin is usually the requesting school or bus barn even when the sheet omits it (`OriginName` / `OriginLocationId`).

## Status

```text
MissingInfo → Draft (Scheduled) → Assigned → Confirmed → Completed
                                   ↘ Changed ↗
         any non-terminal → Cancelled
```

| Status      | Meaning                                                                                             |
| ----------- | --------------------------------------------------------------------------------------------------- |
| MissingInfo | Ticket (and often PAX) exist; place and/or time still unknown. Keep the row.                        |
| Draft       | Scheduled request with enough to show on the board; bus/driver not both assigned. Alias: Scheduled. |
| Assigned    | Bus and/or driver proposed.                                                                         |
| Confirmed   | Validated destination + times + driver + bus accepted.                                              |
| Changed     | Confirmed trip whose time, place, bus, or driver was edited. Keep prior values in history.          |
| Cancelled   | Will not run. Remains on the calendar.                                                              |
| Completed   | Ran (or was closed after return).                                                                   |

## Relationships

- Trip `1` → `0..1` origin location (often a `School` or `Depot` / bus barn).
- Trip `1` → `0..1` destination location (`School` or `TripDestination`) — required to **confirm**, not to **save**.
- Trip `1` → `0..1` assigned Bus (required to confirm; omit when `IsMultiAsset`).
- Trip `1` → `0..1` assigned Driver (required to confirm).
- Trip `1` → `0..1` linked Trip (`LinkedTripId`) for Day 2 or shared ride.
- Trip `1` → `0..*` manifest entries (optional; PII — do not commit).
- Trip does **not** own the daily route. Home route assignments stay on Route.

## How this differs from a route

|            | Route                           | Trip                                   |
| ---------- | ------------------------------- | -------------------------------------- |
| Cadence    | Daily, published                | One date (plus optional return)        |
| Riders     | Assigned students AM/PM         | Named group + PAX; optional manifest   |
| Times      | Predictable stop times          | Expected to move                       |
| Cancel     | Rider exception or rare rewrite | First-class status                     |
| Bus/Driver | Home assignment for the year    | Loan for the event                     |
| Map        | Year-stable waypoints           | Path for this outing when coords exist |

## Out of scope

- Live tracking of the trip bus.
- Overnight hotel itineraries (`IsOvernightPending` + notes only).
- Parent self-serve signup.
- Auto-creating a daily route from a repeated trip.
- Implementing `ActivityService` / `ActivityScheduleService` as the office trip board.
- Committing the office CSV or manifests to git.

## Code anchors

| Spec term             | Existing code                                                                                                         |
| --------------------- | --------------------------------------------------------------------------------------------------------------------- |
| Trip aggregate        | `BusBuddy.Core.Models.Trips.TripEvent` (extend — do not clone `Route`)                                                |
| Leftover calendars    | `Activity`, `ActivitySchedule`, `ActivityService` — parallel leftover. Board = `ITripEventService`. Do not implement. |
| Places                | `IDestinationService`, `IPickupStopService`, `DestinationTypes.TripDestination`                                       |
| Bus / driver loan     | `IBusService`, driver services                                                                                        |
| Path                  | `IRoutingService`, `IMapsGeoService`                                                                                  |
| Trip ticket PDF       | `PdfReportService.GenerateTripTicket` + `PdfPreviewWindow`                                                            |
| Driver hours          | `TripEvent.DriverHours` (duration + `PrePostTripInspectionHours`)                                                     |
| Clerk purpose catalog | `ITripReasonCatalog` (`user-settings.json` keys `Trip.Purposes` / `Trip.Sports`)                                      |
| Same-day fleet        | `IRouteOptimizationService` + `ITripEventService.SuggestSameDayFleetAsync` (does not set `RouteId`)                   |
| Map                   | `MapViewModel` when a trip is selected                                                                                |
| AutoMapper (not maps) | `MappingService`                                                                                                      |

## Worked examples

- Volleyball away with ticket, place, times, driver, bus: upsert by ticket; status `Assigned` until clerk confirms.
- Playoff with ticket and PAX, no place/time: save as `MissingInfo`.
- “Can we adjust leave time to 1:30” in notes: leave `PickupTime` as Departs until a clerk edits it.
- Day 2 row without a ticket: own Trip linked via `LinkedTripId`.
- “All buses and SPED”: `IsMultiAsset`, no silent single `BusId`.
- Field trip destination in notes (6634 County Road HH): `DestinationName` from notes; `DestinationLocationId` only after validation.

## Agent instructions

When changing trip code, read this file plus `specs/locations.md`, `specs/drivers.md`, `specs/buses.md`, `specs/routes.md`, and `specs/maps.md`. Quote the invariant you implemented. Never merge trip scheduling into the published daily route editor. Never add `IsTrip` to `Route`.
