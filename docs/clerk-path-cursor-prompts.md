# Clerk-path Cursor prompts — BusBuddy-3 prod walk

Use these **one item at a time**. Do not start the next prompt until the current item’s **Guest proof** is pasted back with log lines.

Repo: https://github.com/Bigessfour/BusBuddy-3  
App: Syncfusion WPF, .NET 9, Windows desktop. Not AWS.  
Fixture route: **AM Special Needs Bus 5**, school year 2026-2027, 14 published stops.  
Default map center: **38.0872, -102.6208** (Lamar/Wiley CO).

---

## Shared header (paste above every item)

```text
You are working in BusBuddy-3 (Bigessfour/BusBuddy-3).

This is a Syncfusion WPF .NET 9 desktop app that runs on a Windows machine (UTM guest C:\dev\BusBuddy-3). It is NOT hosted on AWS. Do not add Amplify, Lambda, Mapbox, ArcGIS Online, a browser map, or live fleet GPS.

Canonical domain specs (win over leftover UI and over numbered specs/00x-* folders):
- specs/README.md
- specs/students.md
- specs/locations.md
- specs/routes.md
- specs/buses.md
- specs/drivers.md
- specs/trips.md
- specs/maps.md
- .specify/memory/constitution.md

Hard rules for every change:
- Route != Trip. Never add IsTrip to Route.
- PickupMode is Home or CatalogStop. Special needs => home pickup on a special-needs route.
- Same-day "not riding" is a rider exception (date + student), not a route rewrite and not a hard-delete of the year assignment.
- MappingService.cs and BusBuddy.WPF/Mapping/MappingProfile.cs are AutoMapper. Never put tiles, geocode, or Routes API code there.
- Maps display: Syncfusion SfMap only. Official Google Map Tiles (POST createSession + GET tile.googleapis.com/v1/2dtiles/{z}/{x}/{y}?session=&key=). Do not use mt1.google.com scraper URLs. Resolve the README "no OSM" vs maps.md "OSM fail-open" conflict only when asked in item 7; otherwise do not change tile policy.
- Plot only LocationCoordinate.IsValidated points. No 0,0. No MapDefaults US centroid as a pin or as the district home view.
- Live GPS stays deferred. Do not enable IsLiveTrackingEnabled or a fake movement timer to "finish" anything.
- Soft-retire routes; do not hard-delete if FK_Schedules_Route Restrict fires.
- Factory-per-operation DbContext. Do not inject a shared BusBuddyDbContext into long-lived services that run concurrent ops.
- Never commit student PII. Load roster via clerk forms + Google Address Validation.
- Smallest change that satisfies the quoted invariant. No new parallel service, DTO, or map coordinator. Delete unused writers in the same PR (constitution IV — do not rename *.disabled).
- Quote the spec invariant you implemented in the PR/summary.
- Do not start other clerk-path items. Do not "while I am here" satellite tiles, Route Optimization, Families, School Calendar, AI Insights, or Hop 4 RouteAssignments schema.

When code contradicts a MUST / MUST NOT:
1. Name file + symbol.
2. Propose the smallest change that makes code match the spec.
3. Wait only if the spec itself is wrong; otherwise implement the spec.

Guest proof is mandatory. Testhost passing without a UTM cold-start log is NOT done.
Cold start: quit Debug, rebuild this branch, launch from bin\Debug\net9.0-windows (Start In set so Serilog is not C:\Windows\logs).
```

---

## Item 1 — Student in (validated address + pickup mode)

```text
[PASTE SHARED HEADER]

WORK ONLY ITEM 1: Student intake.

Read: specs/students.md, specs/locations.md.

Goal
A clerk can create or edit a student so that:
- Name, school (IDestinationService), and pickup mode persist.
- PickupMode is Home or CatalogStop only.
- Special needs => home pickup on a special-needs route (do not force a catalog stop).
- Street address is incomplete until Google Address Validation + Geocoding succeed.
- Latitude/Longitude are written only from that result.
- Failed validation stays editable, shows "needs validation", and does NOT create a map pin.
- No mock student / LoadMockData path remains on this form.

Code anchors (extend, do not replace):
- IStudentService, Core Student
- PlacesAddressBox + existing PlacesAddressAutocompleteCoordinator / PlaceAddressApplier
- LocationCoordinate.IsValidated
- StudentForm / StudentsView (live surfaces only)

Do not:
- Plot the student from the form with MapDefaults fallback coords
- Invent a second address box
- Touch MapViewModel tile bootstrap
- Import the paper roster xlsx into git

Implement
1. Find the live student create/edit path. List any stub, mock, or unvalidated save.
2. Bind address to PlacesAddressBox. Block save until validated coords exist when the student will be routed or plotted.
3. Persist pickup mode correctly. Special-needs flag must not silently switch mode to CatalogStop.
4. Delete or stop calling any LoadMockData / "Mock Elementary" fallback on this path.

Guest proof (paste back)
- Steps you clicked
- Student id or initials only (no full address in the chat if it is real PII)
- Log lines showing validation success or "needs validation"
- Confirm no pin written for a failed address
- Testhost: PlacesAddressSurfaceTests (must still pass)

Done when: a new student cannot be used as a route waypoint without validated lat/lng, and pickup mode matches the spec.
```

---

## Item 2 — Schools and catalog stops exist and geocode

```text
[PASTE SHARED HEADER]

WORK ONLY ITEM 2: Places — schools + catalog pickup stops.

Read: specs/locations.md, specs/maps.md (plot rules only).

Goal
District-owned School and PickupStop records are named places with validated address + lat/lng before they can be route waypoints or map pins.
Incomplete places stay in the grid as "needs validation" and are skipped by the map.

Code anchors:
- IDestinationService + Core Destination (schools / trip places)
- IPickupStopService + Core PickupStop (catalog stops)
- SchoolDestinationForm, PickupStopForm
- LocationTypes (School, PickupStop, …)
- LocationCoordinate.IsValidated

Do not:
- Treat a StudentHome as a shared PickupStop unless a clerk publishes it
- Type lat/lng as source of truth
- Merge Destination and PickupStop into one table
- Draw pins from this item (that is item 7). Only guarantee the data gate.

Implement
1. Audit SchoolDestinationForm and PickupStopForm save paths. Save must be incomplete without Address Validation + geocode.
2. Any list/API that feeds the map or route builder must filter !LocationCoordinate.IsValidated.
3. Soft-retire (Active=false). Do not delete a place referenced by RouteStop.
4. Confirm PlaceId / formatted address is stored when the API returns it.

Guest proof
- Create or open one school and one catalog stop
- One deliberate bad address: stays needs-validation, no coords
- One good Wiley-area address: lat/lng persisted
- Query or log: validated vs skipped counts
- Testhost: DestinationServiceTests, PickupStopServiceTests, PlacesAddressSurfaceTests

Done when: unvalidated places cannot become waypoints; validated school + stop exist for Bus 5 AM.
```

---

## Item 3 — Publish a route (Bus 5 AM fixture)

```text
[PASTE SHARED HEADER]

WORK ONLY ITEM 3: Published route + ordered stops + drive path + clocks.

Read: specs/routes.md, specs/locations.md, specs/maps.md (polyline source of truth).

Goal
AM Special Needs Bus 5 (2026-2027) is a published Route for one session (SpecialNeeds or AM as modeled), with:
- Ordered RouteStop list (sequence 1..n)
- Each stop a validated location (school, catalog stop, or StudentHome for SN home pickup)
- Target / published arrival (and departure if Core has it)
- Default bus + default driver recommended
- Drive path from Google Routes via RouteDrivePathRefresher / IRoutingService
- PathDistance / PathDuration stored on the route version
- Published clocks EQUAL that path, not a +1 minute loop

Known defect to kill: schedule smoke showed FirstLast=07:30 → 07:43 (14 consecutive one-minute slots) while drive path was DistanceMeters≈42069 Duration≈3580s (~60 min). That is a lying schedule. Fix the publisher / clock bind, not the PDF preview.

Code anchors:
- Core Route, RouteStop
- IRouteService, IGeoDataService, IRoutingService, RouteDrivePathRefresher
- RouteAssignmentView / RouteManagementView only as needed to save stops and refresh path
- ScheduledArrival / ScheduledDeparture (not EstimatedDuration, not UTC Estimated* as display)

Do not:
- Rewrite published times with IRouteOptimizationService
- Use live GPS or Haversine as source of truth
- Hard-delete the route
- Invent a second stop list

Implement
1. Load Bus 5 AM. Print stop count, sequences, and each published clock from the DB.
2. Find what writes 07:30..07:43. Replace with published arrival from RouteStop after Routes refresh (or compute from first stop + path legs — spec: target time per stop is required).
3. After stop order changes, refresh path. Stale polyline is a bug.
4. Mid-year structural edit = new version / effective date if that code exists; do not silently rewrite history. If versioning is incomplete, say so and implement the minimum so current published row is consistent — do not build a full versioning product in this item.

Guest proof
- Log: Drive path computed Stops=14 DistanceMeters=… Duration=…
- Log or grid: 14 clocks whose first→last span is consistent with Duration (not 13 minutes)
- Route published + active
- Testhost: existing Route* tests that assert published clocks / path refresh

Done when: Bus 5 AM has 14 validated stops and clocks a substitute would believe.
```

---

## Item 4 — Assign / remove / not riding today

```text
[PASTE SHARED HEADER]

WORK ONLY ITEM 4: Route assignment roster actions.

Read: specs/routes.md, specs/students.md.

Goal
On RouteAssignmentView for a selected published route:
- Assign Student moves a real unassigned student onto the route session and persists via IRouteService
- Remove Student returns them to unassigned without deleting the student
- Not riding today writes a rider exception for THAT DATE only; year assignment remains
- Add Stop opens RouteStopEditDialog, requires Places lat/lng, then AddStopToRouteAsync
- Grids: Unassigned Students Grid, Assigned Students Grid, Route Stops Grid bind real collections (no mock)
- AutomationProperties.Name stay on those grids and on ButtonAdv labels so UIA can find them (do not "fix" misses by removing names)

Code anchors:
- RouteAssignmentView.xaml / .xaml.cs
- RouteAssignmentViewModel (+ Commands / Schedule partials)
- IRouteService.AssignStudentToRouteAsync, GetStudentsForRouteAsync, GetUnassignedStudentsAsync
- RouteRiderException
- RouteStopEditDialog + Places AddressApplied

Do not:
- Restore LoadMockData or "Mock Elementary School" on service failure
- Treat not-riding as a route version or as deleting the assignment
- Drive the map except as a side effect of existing selection
- Fight the UTM mouse script with coordinate-only buttons; prefer real ButtonAdv Command + AutoName

Implement
1. Grep LoadMockData and any catch that replaces routes with mock data. Delete that path. If IRouteService is missing, leave DataContext null and show the red banner (already specified in docs/action-items).
2. Confirm each of the four commands hits the database and refreshes both grids.
3. Rider exception: date + student + optional reason; load count / today's roster omit them; published stop times unchanged.
4. Add Stop: block save until AddressApplied has coords.

Guest proof
- Assign one student, restart view, still assigned
- Remove that student, they reappear unassigned
- Not riding today: exception row exists; they disappear from today's roster only
- Add Stop rejected without a picked place; accepted with coords
- Logs: RouteAssign ButtonAdv / RouteAssign Click for those buttons (not only CLICK-FALLBACK)
- Testhost: RouteAssignmentViewTests, ApplyClerkOverride tests if present

Done when: all four actions persist and survive a view reload. 13/16 UIA clicks is NOT the done bar — persistence is.
```

---

## Item 5 — Refresh assignment data

```text
[PASTE SHARED HEADER]

WORK ONLY ITEM 5: Refresh Route Data actually completes.

Read: specs/routes.md. Code: RouteAssignmentViewModel.Commands.cs.

Goal
The assignment toolbar action "Refresh Route Data" reloads routes, assigned students, unassigned students, and stops for the current selection from services (not memory), then logs exactly:

  Data refreshed successfully

Smoke on 2026-09-17 scored Refresh Route Data=1 and Data refreshed successfully=0. That is this item.

Implement
1. Trace Refresh command CanExecute and the full try/catch. Why success is skipped (null SelectedRoute, swallowed exception, different log wording, command never invoked).
2. Keep the success string stable — Scripts/score-route-assignment-logs.ps1 keys on it.
3. On failure, log error with exception; do not claim success; do not load mock data.
4. Use factory-per-op DbContext so concurrent refresh + log does not hit EF ConcurrencyDetector (known UTM issue with shared context in ActivityLogService — do not copy that pattern).

Do not retune the UIA scorer to hide a missing success line.

Guest proof
- Cold start, open assignment, select Bus 5 AM, click Refresh Route Data
- Extract contains Data refreshed successfully
- Grids match DB after an external change (or after assign from item 4)
- runtime-errors empty

Done when: the score token is produced by a real reload, not by changing the scorer.
```

---

## Item 6 — Print schedule sheet

```text
[PASTE SHARED HEADER]

WORK ONLY ITEM 6: Route schedule window + printable sheet.

Read: specs/routes.md, specs/maps.md (snapshot caption only).

Goal
ScheduleButton on assignment opens RouteScheduleWindow bound to RouteScheduleViewModel for the selected published route.
- Title from DisplayNameFor (e.g. AM Special Needs Bus 5)
- Stop list / clocks from published ScheduledArrival / ScheduledDeparture in clock order
- First→last span consistent with stored PathDuration from item 3
- Print / preview uses RouteSummaryPdfRenderer PdfGrid
- Caption if a map is embedded: published path, not live tracking
- MapEmbedded may be false on View Schedule; do not block this item on Print Map snapshot
- Dashboard RouteSummary PDF must not be a 1606-byte stub if this print path shares that renderer — if it does, fix the shared renderer; if it does not, leave dashboard reports for a later release and say so

Code anchors:
- RouteScheduleWindow, RouteScheduleViewModel
- RouteAssignmentViewModel.Schedule.cs (Schedule / Report wrappers)
- RouteSummaryPdfRenderer / PdfGrid
- PdfPreviewWindow (DataContext null on the viewer chrome is OK)

Do not:
- Use Process print verb as the only preview
- Reintroduce EstimatedDuration as the displayed clock
- Enable live tracking to fill the sheet
- Spend this item on UIA clock text scraping (uiaClockTexts empty is harness)

Implement
1. Bind the schedule UI to published stop times from item 3.
2. Preview PDF: Stops=14, readable grid, bytes >> 2 KB.
3. If Print Map / RequestMapSnapshot is already wired, leave it; do not resurrect Measure/Arrange(Infinity). If it is broken, leave MapEmbedded=false and note it as deferred — sheet without map is acceptable for this item.

Guest proof
- Log: Opened route schedule DisplayName=AM Special Needs Bus 5 Stops=14 FirstLast=<real span> Clocks=<14 times>
- Log: Rendered PdfGrid route sheet … Bytes=<not 1606>
- previewLoaded=true, binding errors=0
- Open the PDF on the guest and confirm times match the window

Done when: a substitute can hold the sheet and the times match the published path.
```

---

## Item 7 — District Map

```text
[PASTE SHARED HEADER]

WORK ONLY ITEM 7: District Map display.

Read: specs/maps.md, specs/locations.md, specs/routes.md (polyline source).

Goal — four operations only, one writer each:
1) Tiles: MapTileBootstrap + GoogleMapTilesImageryLayer + GoogleMapTileSessionService
   POST tile.googleapis.com/v1/createSession then UrlTemplate
   https://tile.googleapis.com/v1/2dtiles/{z}/{x}/{y}?session=&key=
   Default center 38.0872, -102.6208. Not MapDefaults US centroid.
2) Pins: one host (MapMarkerHost + MapMarkerLabels.Kind)
   Always: validated schools + catalog stops
   Selected route: that run’s stops + eligible homes
   Skip invalid coords
3) Path: RouteDrivePathRefresher → RouteLinePoints → MapRouteTrail / MapRouteTrailLayer
4) Camera + optional snapshot in MapView.xaml.cs only

Resolve spec conflict in this item and only this item:
- specs/README.md says Google tiles only (no OSM)
- specs/maps.md says OSM fail-open
Pick ONE, edit both files to match, implement that choice.
If fail-open: OSM only after session or first tile GET fails; log Host=DistrictMap Outcome=ok|osm-failopen|error
If no OSM: log error and show status; do not silently use mt1.google.com

Do not:
- Add a second ImageryLayer / GetUri scrape path
- Let MapStudentPlot, MapDistrictLayers, StudentsMapCoordinator, or StudentFormMapCoordinator write pins if MapMarkerHost already can — fold or delete the extra writer
- Enable IsLiveTrackingEnabled
- Edit MappingService.cs
- Build satellite as a third product in this item (Road vs Satellite in MapBasemap may exist; default Roadmap)

Implement
1. Inventory who mutates SfMap. Keep one tile writer, one marker writer, one trail writer.
2. ResetView / first open uses DistrictMapAnchor / Wiley constants.
3. Select Bus 5 AM: fit path, one polyline, stop pins, no stacked WP+home duplicates (tag existing pin with RouteStopLabel).
4. Delete unused map helpers in the same change.

Guest proof
- Cold start District Map
- Log: MapTileBootstrap Host=DistrictMap Outcome=ok (or osm-failopen if that is the chosen policy)
- Visual: Wiley/Lamar area, school + stop pins
- Select Bus 5 AM: one gold path, Duration consistent with item 3
- Testhost: MapViewTests, MapViewModelTests, MapToolbarSmokeTests, DistrictMapAnchorTests, GoogleMapTileSessionTests
- Tools/SfMapTileProbe or MapsConnectionProbe: one Wiley tile with the real key

Done when: those four operations work and extra writers are gone or unreferenced.
```

---

## Item 8 — Bus number + capacity warning

```text
[PASTE SHARED HEADER]

WORK ONLY ITEM 8: Bus asset + capacity vs session roster.

Read: specs/buses.md, specs/routes.md (capacity warning), specs/drivers.md only if default driver pairing is already on the same dialog.

Goal
- Visible bus number is the student-facing key
- VehicleType Regular vs SpecialNeeds (lift / wheelchair stations as applicable)
- SeatedCapacity required
- Assigning riders to a route whose default bus would overflow WARNS; it does not silently assign
- HomeRouteId default pairing for the year; spare/sub does not rewrite that default
- No live XY on Bus; map may label "#5" on the path only

Code anchors:
- IBusService, Core Bus
- VehicleManagementView / VehicleManagementViewModel (live surface)
- Route capacity check on assign / auto-assign

Do not:
- Finish Fuel / Maintenance / pre-trip child grids unless they block number+capacity save
- Chase VehicleForm DataContext=null chrome except: if Save lives on that host and cannot persist, bind or move Save to the VM that works
- Add telematics
- Treat Vehicle as a second model if Bus is the spec type — extend Bus

Implement
1. Confirm Bus 5 exists with number, type SpecialNeeds, capacity, Active, HomeRoute pointing at the SN AM route when that is the district pairing.
2. Find overflow check. If missing, add a warning on assign when assigned count > seated (+ wheelchair rules if already modeled). Do not invent a full load-planning engine.
3. VehicleMgmt audit MissingAutomationName=1 NoCommand=1: name the control and bind Command or remove it.
4. Leave inspection/fuel records as existing child types; do not build the shop module in this item.

Guest proof
- Bus 5 row visible with number + capacity
- Assign one more rider than capacity (or a testhost equivalent) => warning, no silent extra assign
- Log: Loaded N vehicles / retrieved buses
- Testhost: existing IBusService / capacity tests; add one if none assert the warning

Done when: clerks see the number students quote and overflow cannot hide.
```

---

## Item 9 — Trip is not a route

```text
[PASTE SHARED HEADER]

WORK ONLY ITEM 9: One-date trip vs published route.

Read: specs/trips.md, specs/routes.md, specs/locations.md (TripDestination).

Goal
A trip (athletic / field / club outing) is Core Trips.TripEvent for one date with origin + destination locations, manifest, and status.
It MUST NOT be stored as Route with an IsTrip flag.
Loan of a bus or driver to a trip does not rewrite the year home route / home driver.
Map may plot that outing’s origin/destination/path when a trip is focused, without destroying the published daily map idea — only if that code already exists. Do not add a second map stack.

Code anchors:
- BusBuddy.Core.Models.Trips.TripEvent, TripStatus
- ITripEventService
- Existing trip UI (TripEvent board / edit dialog)
- Known local-only bug to avoid: TripEventEditDialogViewModel read-only *Display properties TwoWay-bound — bind OneWay if you touch that dialog

Do not:
- Add IsTrip to Route
- Use daily route stop times as the trip sheet
- Enable live GPS for the outing
- Rebuild the whole activity module; one create → confirm → cancel (or equivalent status) path is enough

Implement
1. Grep IsTrip, RouteType=Trip, and any Route used as an outing. Remove or stop writing that shape.
2. Create one trip to a validated TripDestination (or school). Persist date, places, status.
3. Cancel / close must not delete the published daily route or its assignments.
4. If trip edit dialog is broken on StatusDisplay TwoWay, fix to OneWay display fields.

Guest proof
- New TripEvent id + date + destination name (no student PII)
- Routes grid still has Bus 5 AM unchanged
- No IsTrip column populated
- Testhost: trip service tests; Route vs Trip assertions if present

Done when: clerks can record an outing without corrupting published runs.
```

---

## After each item (operator, not Cursor)

On the UTM guest:

1. Quit Debug. Rebuild the branch. Launch with working directory = bin\Debug\net9.0-windows.
2. Perform the Guest proof steps in the item.
3. Pull Serilog from the app logs folder (not C:\Windows\logs unless Start In was wrong).
4. Paste into the next chat: item number, pass/fail, log excerpt, files changed.
5. Only then paste the next item prompt.

Deferred on purpose (do not put in these nine prompts): live fleet GPS, Families/Guardians, School Calendar, AI Insights, Route Optimization authorization, Hop 4 RouteAssignments schema, fuel/shop module, dashboard mock-AI RouteSummary if it is not the schedule renderer.

If Cursor adds files that are not the single writer for tiles, markers, trail, Places, or the named Core service, require they be deleted before the item is marked done.
