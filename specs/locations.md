# Locations

## Purpose

A location is a named place BusBuddy-3 can send a bus to, pick students up from, or use as a district facility. Locations are the geographic facts behind students, routes, and maps. Trips are not locations; they _use_ locations. See `specs/trips.md`.

BusBuddy-3 is a Syncfusion WPF .NET 9 desktop app on Windows. It is not hosted on AWS. Do not add a cloud gazetteer or a second map stack to satisfy this spec.

## Invariants

- MUST classify every location with exactly one type: `School`, `PickupStop`, `StudentHome`, `Depot`, `Maintenance`, `Fuel`, `TripDestination`.
- MUST persist a validated street address plus lat/lng before a location can be a route waypoint, map pin, or trip origin/destination.
- MUST use Google Address Validation as the source of “this address exists,” and that result’s `geocode.location` as the default pin. Clerks do not type lat/lng as truth. Places Autocomplete fills the form; it does not place the pin. A student-home boarding point may be a clerk map adjustment stored on the student (`HomePickupClerkAdjusted`). Re-validation of the same street does not replace that point. A new street address clears it.
- MUST plot only building-level geocodes (`geocodeGranularity` PREMISE / SUB_PREMISE / PREMISE_PROXIMITY) with a confirmed street. Unconfirmed `route`/`street_number`, or a geocode whose `placeTypes` are only locality/political, is city-level and is not a pin. ROUTE and OTHER are not pins. Geocoding fallback (when Address Validation is 403) may pin ROOFTOP `street_address` / `premise` only — not RANGE_INTERPOLATED, APPROXIMATE, or locality.
- MUST treat district-owned facilities (`School`, `Depot`, `Maintenance`, `Fuel`, published `PickupStop`) as stable for the school year by default.
- MUST allow `StudentHome` to change mid-year (family move). Keep the student; replace or version the home location.
- MUST NOT use an unvalidated address as a map pin (no 0,0, no US centroid, no “close enough” guess).
- MUST NOT treat a student’s home as a shared catalog stop unless a clerk publishes that home as a `PickupStop`.
- MUST NOT fold trips into this entity. A trip is a one-time movement that _references_ two or more locations.
- MUST NOT invent a parallel place model. Extend `IDestinationService`, `IPickupStopService`, `IGeoDataService` / `IMapsGeoService`, and `DistrictDepot`.
- Unconfigured map center is the `MapDefaults` US overview. A district center exists only after the clerk sets a depot, bounding box, or school.
- Exception: facility address correction, new catalog stop, school-year rollover, or a trip to a one-off destination (field, another district, event site).

## Relationships

- `School` ← assigned destination for students; common route end (AM) or start (PM).
- `PickupStop` ← shared in-town gathering point; route waypoint; many students.
- `StudentHome` ← one student (or siblings at the same address); waypoint only when pickup mode is `Home` or special needs. That waypoint is the student's stored home coordinate.
- `Depot` / `Maintenance` / `Fuel` ← district operations; optional route start/end or deadhead, not a student stop.
- `TripDestination` ← activity/athletic/field site; used by trips, not by the published daily route unless it is also a school.
- Location `1` → `0..*` route stops (ordered waypoints on a route session).
- Location `1` → `0..*` trip origins or destinations.

## Data the app must store

| Field                         | Type         | Required       | Notes                                                                                 |
| ----------------------------- | ------------ | -------------- | ------------------------------------------------------------------------------------- |
| LocationId                    | existing key | yes            | Reuse Core identity.                                                                  |
| Name                          | string       | yes            | “School”, “Main & 4th”, “Bus Barn”.                                                   |
| LocationType                  | enum         | yes            | See types above.                                                                      |
| StreetAddress                 | string       | yes            | Human-entered; then validated.                                                        |
| City / State / PostalCode     | string       | yes            | Colorado district context; do not assume Denver.                                      |
| FormattedAddress              | string       | after validate | From Google Address Validation.                                                       |
| Latitude, Longitude           | decimal      | after validate | From geocoder.                                                                        |
| PlaceId or validation payload | string/json  | recommended    | Enough to re-validate later.                                                          |
| IsDistrictFacility            | bool         | yes            | True for school, depot, maintenance, fuel, published stops.                           |
| SchoolYearStable              | bool         | yes            | Default true for district facilities; false for student homes and one-off trip sites. |
| Active                        | bool         | yes            | Soft-retire. Do not delete if routes/trips reference it.                              |
| Notes                         | string       | no             | Access notes: driveway, gate, dirt road, no turnaround.                               |

Do not store student names on a location. Homes point _from_ the student record.

## Behaviors / UI

- Clerk records a place on a location form. Save is incomplete until Address Validation + geocode succeed.
- Clerk can add, edit, or remove a school campus from Student Management. Edit covers name, address, GPS, and bell times. Unused campuses may be deleted; campuses still referenced by students, transfers, activities, or trips are retired (`Active=false`) instead of deleted.
- Failed validation stays editable and is not plotted. The clerk message must start with **Rejected**, say **no map pin**, and name the next step (correct house number/street, or pick a Google suggestion, then click Validate Address). Do not use “needs validation” as the only signal on the Validate result — that phrase is the saved-record incomplete badge, not a next action.
- District Map plots schools, catalog stops, and geocoded student homes. Depots may plot as operational markers.
- Route builder adds locations as ordered stops; those stops become Google Routes waypoints.
- Substitute drivers use the same published locations and times. Do not give them a shadow set of “unofficial” pins.
- Mid-year new catalog stop: new `PickupStop`, validate, then clerk attaches students and updates the route version. The student form may **hint** that several Home pickups sit near each other; it must not publish the stop for the clerk.
- Mid-year student move: new or updated `StudentHome`, validate, re-attach student; do not silently move the old pin.

## Pickup and drop-off use

| Type            | Daily route                                     | Trip             | Map                                             |
| --------------- | ----------------------------------------------- | ---------------- | ----------------------------------------------- |
| School          | Yes — destination (AM) / origin (PM)            | Often origin     | Always if active                                |
| PickupStop      | Yes — in-town pickup/drop                       | Rare             | Always if active                                |
| StudentHome     | Yes — when pickup mode is Home or special needs | Rare             | Only if geocoded and relevant to selected route |
| Depot           | Deadhead / pull-out / return                    | Possible staging | Operational                                     |
| Maintenance     | Not a student stop                              | No               | Operational                                     |
| Fuel            | Not a student stop                              | No               | Operational                                     |
| TripDestination | No                                              | Yes              | When a trip is selected                         |

## Out of scope

- Live GPS of buses at these places.
- Drawing unofficial stops that skip validation.
- Treating “trip” as a location type.
- Auto-creating catalog stops from every student home.
- AWS geocoders or a second tile vendor as source of truth (basemap is Google Map Tiles only).

## Code anchors

| Spec term               | Existing code                         |
| ----------------------- | ------------------------------------- |
| Schools / destinations  | `IDestinationService`                 |
| Published catalog stops | `IPickupStopService`                  |
| Waypoints / geo access  | `IGeoDataService`, `IMapsGeoService`  |
| Depot                   | `DistrictDepot`                       |
| Map pins and center     | `MapViewModel`, SfMap                 |
| Address → lat/lng       | Google Address Validation + Geocoding |
| AutoMapper (not maps)   | `MappingService`                      |

## Agent instructions

When changing location or geocode code, read this file plus `specs/students.md`, `specs/routes.md`, `specs/trips.md`, and `specs/maps.md`. Quote the invariant you implemented. If a task is about a sports run, field trip, or one-time manifest, that work belongs in `specs/trips.md` — do not overload Location.
