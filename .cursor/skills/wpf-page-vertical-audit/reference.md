# WPF Page Vertical Audit — Reference

## Student views folder (`Views/Student/`)

| Surface                     | Role               | Status                                         |
| --------------------------- | ------------------ | ---------------------------------------------- |
| `StudentsView`              | List hub + toolbar | **Keep** — P1 VM smoke                         |
| `StudentForm`               | Add/edit dialog    | **Keep** — audited                             |
| `SchoolDestinationForm`     | Add school (Hop 1) | **Keep**                                       |
| `PickupStopForm`            | Add pickup stop    | **Keep**                                       |
| `StudentSchoolTransferForm` | School transfer    | **Keep** — `IsEditable=False` on school combos |
| ~~`QuickActionsDialog`~~    | Removed 2026-09-03 | Placeholder; broken owner context              |

### Students code-complete checklist (2026-09-03)

| Item                                                              | Status              |
| ----------------------------------------------------------------- | ------------------- |
| `StudentSchoolLinker` + grid school `DestinationId` sync          | Done                |
| Grid/bulk/delete/load via `IStudentService`                       | Done                |
| `StudentForm` geocode on validate + save                          | Done                |
| `StudentForm` Places Autocomplete (US4)                           | Done                |
| CSV import sole-school `DestinationId`                            | Done                |
| `SchoolCatalogChangedMessage` / `PickupStopCatalogChangedMessage` | Done                |
| Hop2 grid columns (`DestinationId`, lat/lon)                      | Done                |
| `BusBuddyInputStyles` + `InputCaretHelper` app-wide               | Done                |
| Structured Serilog on student load/save/delete/bulk/validate      | Done                |
| VM smoke Hops 1–2                                                 | **Windows VM only** |

Grades: `StudentGradeCatalog.All` in Core — shared by form, grid, and `StudentService`.

MainWindow Add/Edit Student → `StudentsView` startup → `StudentForm` (single clerk path).

## StudentForm (Add Student dialog)

### File map

| Layer       | Path                                                                                        |
| ----------- | ------------------------------------------------------------------------------------------- |
| View        | `BusBuddy.WPF/Views/Student/StudentForm.xaml`                                               |
| Code-behind | `BusBuddy.WPF/Views/Student/StudentForm.xaml.cs`                                            |
| ViewModel   | `BusBuddy.WPF/ViewModels/Student/StudentFormViewModel.cs`                                   |
| Entity      | `BusBuddy.Core/Models/Student.cs`                                                           |
| Service     | `BusBuddy.Core/Services/StudentService.cs`                                                  |
| Normalizer  | `BusBuddy.Core/Utilities/StudentRecordNormalizer.cs`                                        |
| Field keys  | `BusBuddy.WPF/Utilities/StudentFormFields.cs`                                               |
| EF config   | `BusBuddyDbContext` → `Entity<Student>`                                                     |
| Migrations  | `20260902183000_StudentFamilyIdDropDefault`, `20260817140000_StudentContactFieldsAlignment` |

### Validation layers (must stay aligned)

| Layer                          | What it checks                        | Blocks save?                            |
| ------------------------------ | ------------------------------------- | --------------------------------------- |
| XAML `ValidationRules`         | Name, address, city, zip non-empty    | Binding errors only                     |
| VM `IsValidStudent()`          | Name + grade                          | Yes                                     |
| VM `ValidateAddressAsync`      | Maps / local format                   | Warn only (non-blocking on save)        |
| Service `ValidateStudentAsync` | Routes exist, phones, zip, uniqueness | Yes (via dry-run before persist)        |
| Postgres                       | FK, unique, timestamptz               | Yes (exception → `DatabaseUserMessage`) |

### Save pipeline

```
SaveCommand → SaveStudentAsync
  → ClearAllFieldErrors
  → IsValidStudent (name + grade)
  → ValidateWithServiceAsync (dry-run when IStudentService available)
  → optional address validation (non-blocking)
  → Normalize phones/zip + StudentRecordNormalizer
  → CanConnectAsync
  → AddStudentAsync / UpdateStudentAsync
  → StudentSavedMessage + RequestClose(true)
```

### Known failure modes (P0)

1. **Route not in DB** — editable route combo or phantom defaults (`Route A`…) when DB has real routes
2. **FamilyId FK** — legacy default `0`; migration `20260902183000` + normalizer required
3. **DateOfBirth Kind** — Postgres `timestamptz`; `StudentRecordNormalizer.NormalizeDateTimes`
4. **Postgres unreachable** — hybrid VM; check `BUSBUDDY_CONNECTION` and Mac Docker Postgres
5. **Service/VM mismatch** — VM allowed save but service threw; fixed by pre-save `ValidateStudentAsync`

### Syncfusion controls on StudentForm

| Control            | MCP query hint                            |
| ------------------ | ----------------------------------------- |
| `ChromelessWindow` | chromeless window theming SfSkinManager   |
| `SfTextBoxExt`     | two-way Text binding UpdateSourceTrigger  |
| `SfMaskedEdit`     | Value binding MaskType RegEx phone        |
| `ComboBoxAdv`      | SelectedItem ItemsSource IsEditable false |
| `SfDatePicker`     | Value AllowNull UTC date                  |
| `ButtonAdv`        | Label IsDefault command binding           |
| `ToggleButtonExt`  | IsChecked two-way                         |

### RAG queries

```
StudentForm AddStudentAsync ValidateStudentAsync save
Postgres BUSBUDDY_CONNECTION docker-compose profiles
Student FamilyId DestinationId PickupStopId FK
Syncfusion WPF ComboBoxAdv SelectedItem MVVM
```

### Test plan (StudentForm)

- [ ] VM: `SaveStudentAsync` with mock service — invalid route → field error, no exception
- [ ] VM: minimal name+grade → `AddStudentAsync` called once
- [ ] Service: `AddStudentAsync_ValidStudent_PersistsAndSetsDefaults` (existing)
- [ ] VM smoke (Windows): Add Student → Serilog `Successfully saved student` → row in `Students`
- [ ] Numpad: focus ZIP masked edit → NumPad digits appear (app-wide handler)
- [ ] Binding script: `python .github/scripts/audit-wpf-bindings.py BusBuddy.WPF/Views/Student/StudentForm.xaml`

### Numpad / external keyboard

**Root cause:** Syncfusion `SfTextBoxExt` / `SfMaskedEdit` host an inner `TextBox`. NumPad keys are swallowed unless handled on `PreviewKeyDown` on the **inner** caret host.

**Fix:** `NumpadInputHelper.RegisterApplicationWide()` in `App.OnStartup` inserts into focused `TextBox` or compatible Syncfusion host.

**Note:** NumLock must be **on** for numpad digits; with NumLock off, keys send navigation events.

### Log paths (Windows VM)

```
C:\dev\BusBuddy-3\BusBuddy.WPF\bin\Debug\net9.0-windows\logs\runtime-errors.log
C:\dev\BusBuddy-3\BusBuddy.WPF\bin\Debug\net9.0-windows\Logs\busbuddy-*.txt
C:\dev\logs\   (if copied manually)
```

## Vehicle views folder (`Views/Vehicle/`)

| Surface                 | Role                     | Status                             |
| ----------------------- | ------------------------ | ---------------------------------- |
| `VehicleManagementView` | Fleet CRUD (grid+detail) | **Canonical** surface              |
| `VehicleForm`           | ChromelessWindow host    | Hosts `VehicleManagementView`      |
| `VehiclesView`          | Navigation stub          | Embeds `VehicleManagementView`     |
| ~~`BusForm`~~           | Removed 2026-09-03       | Use `VehicleFleetLauncher` instead |

**Single entry:** `VehicleFleetLauncher.ShowDialog(owner, startup)` — MainWindow, QuickActions, Dashboard.

### Vehicle code-complete checklist (2026-09-03)

| Item                                                                   | Status              |
| ---------------------------------------------------------------------- | ------------------- |
| `VehicleManagementViewModel` save via `AddBusAsync` / `UpdateBusAsync` | Done                |
| Delete via `DeleteBusAsync` (no in-memory-only delete)                 | Done                |
| No client-invented `BusId` before persist                              | Done                |
| Structured Serilog (`LogContext` + `BusId` / `BusNumber`)              | Done                |
| `BusForm` removed; `VehicleFleetLauncher` unified entry                | Done                |
| XAML polish (header, toolbar, VIN, empty state)                        | Done                |
| VM smoke Hop 4 — add bus → Serilog `Added vehicle BusId=`              | **Windows VM only** |

### Known failure modes (P0 — fixed 2026-09-03)

1. **Fake BusId** — save assigned `Max(BusId)+1` before calling service → UI showed success without DB row
2. **Swallowed service errors** — empty `catch` on `AddBusAsync` / `UpdateBusAsync`
3. **Delete in-memory only** — `DeleteVehicleAsync` never called `DeleteBusAsync`

### Log tokens (VM smoke)

```
Added vehicle BusId=
Updated vehicle BusId=
Successfully deleted vehicle BusId=
Loaded {VehicleCount} vehicles
```

## Settings view (`Views/Settings/`)

| Surface        | Role                                       | Status                        |
| -------------- | ------------------------------------------ | ----------------------------- |
| `SettingsView` | User preferences (theme, logging, startup) | **Keep** — audited 2026-09-03 |

### Settings code-complete checklist (2026-09-03)

| Item                                                                      | Status              |
| ------------------------------------------------------------------------- | ------------------- |
| All VM preferences bound in XAML                                          | Done                |
| `UserSettingsKeys` shared constants                                       | Done                |
| `EnableActivityLogging` gates `ActivityLogService`                        | Done                |
| `ShowDashboardOnStartup` opens `DashboardView` on `MainWindow_Loaded`     | Done                |
| Theme preview on change; persist on Save / MainWindow theme buttons       | Done                |
| Unified theme load via `IUserSettingsService` in `SyncfusionThemeManager` | Done                |
| Structured Serilog on load/save/reset                                     | Done                |
| `SettingsViewModelTests` + `UserSettingsServiceTests`                     | Done                |
| VM smoke — toggle logging off, restart, confirm no activity rows          | **Windows VM only** |

Persistence: `%AppData%/BusBuddy/user-settings.json` via `IUserSettingsService` (no EF).

## Route views folder (`Views/Route/`)

| Surface               | Role                                        | Status                        |
| --------------------- | ------------------------------------------- | ----------------------------- |
| `RouteManagementView` | Fleet route list + planning toolbar         | **Keep** — audited 2026-09-03 |
| `RouteAssignmentView` | Docked assignment (students/stops/generate) | **Keep** — canonical Hop 3    |
| `RouteStopsEditor`    | Stop list child of assignment view          | **Keep**                      |
| `RouteStopEditDialog` | Single-stop edit dialog                     | **Keep**                      |

**Entry points:** MainWindow **Routes** pane (`RouteAssignmentView`); **Route Management** dialog (`RouteManagementView`); **Manage Route** opens assignment via `RouteAssignmentLauncher`.

### Route code-complete checklist (2026-09-03)

| Item                                                                     | Status              |
| ------------------------------------------------------------------------ | ------------------- |
| `RouteManagementView` bus + time-slot pickers for `AssignVehicleCommand` | Done                |
| `RouteManagementViewModel` load/add/edit/delete via `IRouteService`      | Done                |
| `RouteManagementView` resolves VM from DI                                | Done                |
| `RouteAssignmentLauncher` for modal assignment entry                     | Done                |
| `InitializeAsync` + split `IsBusy`/`IsRefreshing` load gates             | Done                |
| `RouteManagementViewModel` buses via `GetAvailableBusesAsync`            | Done                |
| `RefreshDrivePathCommand` + Routes API via `RouteDrivePathRefresher`     | Done                |
| `RouteAssignmentView` Assign Bus / Assign Driver / Refresh buttons       | Done                |
| Footer shows `SelectedRouteBusDisplay` / `SelectedRouteDriverDisplay`    | Done                |
| `IsLoading` indicator on both route surfaces                             | Done                |
| Structured Serilog (`LogContext` + `ViaService=true` on CRUD)            | Done                |
| VM smoke Hop 3 — Generate Routes → Serilog `Route generation completed`  | **Windows VM only** |
| VM smoke Hop 4 — Assign bus → `Routes.AMVehicleId` / `PMVehicleId`       | **Windows VM only** |

### Known failure modes (P0 — fixed 2026-09-03)

1. **Assign Vehicle with no bus picker** — toolbar button existed but `AvailableBuses` / `SelectedTimeSlot` were not bound in XAML
2. **Direct EF in RouteManagementViewModel** — load/add/delete/edit bypassed `IRouteService` (clone/assign already used service)
3. **RouteAssignmentView missing assign buttons** — `AssignVehicleCommand`, `AssignDriverCommand`, `RefreshDataCommand` existed in VM but not wired

### Log tokens (VM smoke)

```
Loaded {RouteCount} routes ViaService=True
Added route {RouteId}:{RouteName} ViaService=True
Assigned vehicle {VehicleId} to route {RouteId}
Route generation completed
```

## Google Maps Platform (spec 007)

Canonical entry points — do not call `GoogleAddressValidationClient` from WPF VMs directly.

| Layer      | Path                                                   | Role                                                     |
| ---------- | ------------------------------------------------------ | -------------------------------------------------------- |
| Facade     | `BusBuddy.Core/Services/GoogleMaps/IMapsGeoService.cs` | Validate + geocode with cache                            |
| Client     | `GoogleAddressValidationClient.cs`                     | Address Validation API (+ Geocoding fallback)            |
| Routing    | `GoogleRoutingService.cs` / `IRoutingService`          | Routes API `computeRoutes`                               |
| Places     | `GooglePlacesAutocompleteService.cs`                   | Places API (New) autocomplete + details (US4)            |
| Drive path | `RouteDrivePathRefresher.cs`                           | Refresh `Route.WaypointsJson` (fail-open)                |
| Cache      | `MapsAddressCache.cs`                                  | In-memory + `%AppData%/BusBuddy/maps-address-cache.json` |
| Probe      | `.github/scripts/run-maps-connection-probe.sh`         | Live key smoke (Address Validation + Routes)             |

### WPF surfaces wired to Maps

| Surface                          | Maps usage                                                                                |
| -------------------------------- | ----------------------------------------------------------------------------------------- |
| `StudentFormViewModel`           | Validate/geocode on save; `ViewOnMapAsync`; **Places Autocomplete** popup on home address |
| `StudentsViewModel`              | Grid validate address                                                                     |
| `SchoolDestinationFormViewModel` | School GPS on save                                                                        |
| `RouteManagementViewModel`       | **Drive Path** toolbar → `RefreshDrivePathCommand`                                        |
| `MapViewModel`                   | Optional drive-path refresh when plotting route                                           |
| `RouteAssignmentViewModel`       | Plot stops via `IMapsGeoService` when configured                                          |

Key: `GOOGLE_MAPS_API_KEY` (Passwords). Quota project: `busbuddy-507301`. Map tiles remain OSM/SfMap — no Google map tiles.

### VM smoke (Windows only)

1. `.github/scripts/run-maps-connection-probe.sh` — expect OK for Wiley sample address + route
2. Student form — type `100 Main` → Wiley suggestions; pick one → city/state/ZIP filled; Validate before save
3. Student validate/save → lat/lon persisted
4. Route with ≥2 stops → **Drive Path** → polyline in `WaypointsJson`
5. Without key → `MappingUnconfigured` / orange status, no hash scatter
