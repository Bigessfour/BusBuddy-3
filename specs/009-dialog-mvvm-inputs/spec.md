# Feature Specification: Dialog MVVM Inputs

**Feature Branch**: `feature/009-dialog-mvvm-inputs`

**Created**: 2026-09-01

**Status**: Draft (Wave 0 scope lock)

**Input**: Clerks enter buses, drivers, fuel, students, and related records on Syncfusion WPF dialogs. Several of those surfaces are unbound, self-as-DataContext, or dead XAML that agents keep “fixing.” Lock which dialogs to delete, which must bind to a ViewModel, ComboBoxAdv rules, and a shared field style — then remediate one wave per PR.

## Baseline (as of Wave 0, after PR #48 on `master`)

PR **#48** (`fix: data-entry forms audit + Maps Platform key wiring`) is merged. Wave 0 is **docs-only**. Do **not** rewrite DriverForm in this PR.

| Area                              | Current on `master`                                                                                                                                                                                                                | Target                                                                                                                                                                    |
| --------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Route edit / builder UserControls | `RouteEditDialog` (+ `.xaml.cs`) + `RouteForm` exist; no live navigation. `MainWindowClerkPathTests.RouteEditDialog_SaveCommandIsImplemented` still reads those files.                                                             | **Delete** both (and unused `RouteEditDialogViewModel`) **and** update that clerk-path test in Wave 1                                                                     |
| Bus add/edit dialog               | `BusEditDialog` is code-behind (`SfTextBoxExt` names + Click); VM unused, namespace `ViewModels.BusManagement`                                                                                                                     | Bind XAML to `BusEditDialogViewModel` in `ViewModels.Bus`                                                                                                                 |
| Fuel record dialog                | Live; `DataContext = this` on the Window; TwoWay bindings present on several fields                                                                                                                                                | TwoWay confirm (Wave 2); dedicated VM later (Wave 3)                                                                                                                      |
| Vehicle add/edit window           | `VehicleForm` hosts `VehicleManagementView` (fleet grid)                                                                                                                                                                           | Host **BusForm** (add/edit), not the fleet grid                                                                                                                           |
| ComboBoxAdv                       | **DriverForm already** `ItemsSource` + `SelectedItem` (no WPF `ComboBoxItem`, no `SelectedValuePath=Content`). StudentForm still has nested PriorityBinding ItemsSource on Grade/State and `SelectedValuePath=RouteName` on AM/PM. | Keep DriverForm as-is. Other in-scope forms: ItemsSource + SelectedItem (+ DisplayMemberPath for objects). Ban WPF ComboBoxItem children and `SelectedValuePath=Content`. |
| Field chrome                      | DriverForm local `FieldInputStyle` is Height **40**, Padding 8,5. Clerk-path tests assert `Height="40"`. StudentForm / SchoolDestinationForm also Height **40**.                                                                   | Shared `FieldInputStyle`: Height=**40**, Padding=8,5 (match PR #48 + clerk-path tests). Do **not** standardize on Height 32 unless the user explicitly overrides.         |
| School destination                | `SchoolDestinationForm` (+ VM) exists on `master`                                                                                                                                                                                  | Wave 4 hygiene; do not treat as absent                                                                                                                                    |

Constitution: Syncfusion-only UI, Serilog-only logging, hybrid Mac/Windows, no cloud app hosting, no committed secrets. Do not run `specify init --here --force`. Do not touch Route Assignment drag-drop or Maps/007.

## Locked decisions

These are immutable for later waves. Agents MUST NOT invert them while “fixing” XAML.

### 1. RouteEditDialog and RouteForm are orphans — DELETE

Repo-wide `*.cs` / `*.xaml` search (after PR #48):

| Surface                                                                     | Live constructor / navigation?                                                                                                                      | Decision                                                                  |
| --------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------- |
| `BusBuddy.WPF/Views/Route/RouteEditDialog.xaml` + `RouteEditDialog.xaml.cs` | Only self-constructs `RouteEditDialogViewModel`. No `new RouteEditDialog(` outside itself.                                                          | **DELETE**                                                                |
| `BusBuddy.WPF/ViewModels/Route/RouteEditDialogViewModel.cs`                 | Only referenced by the dialog itself. **`BusBuddy.Tests/WPF/MainWindowClerkPathTests.RouteEditDialog_SaveCommandIsImplemented` reads these files.** | **DELETE** with the dialog; Wave 1 **must** update that test              |
| `BusBuddy.WPF/Views/Route/RouteForm.xaml`                                   | No `RouteForm.xaml.cs`, no `partial class RouteForm`, no `new RouteForm(`.                                                                          | **DELETE**                                                                |
| `BusBuddy.WPF/Views/Route/RouteStopsEditor.xaml`                            | **Live** in `BusBuddy.WPF/Views/Route/RouteAssignmentView.xaml`                                                                                     | **KEEP** (not in the delete set; do not touch Route Assignment drag-drop) |

Live route work stays on Route Assignment / Route Management. Do not reimplement RouteEditDialog Save as a substitute for deletion.

### 2. BusEditDialog must bind to BusEditDialogViewModel

- Namespace: `BusBuddy.WPF.ViewModels.BusManagement` → `BusBuddy.WPF.ViewModels.Bus`.
- Same-folder types `BusManagementViewModel` and `BusViewModel` move in the **same wave** so `ViewModels/Bus/` is not split-namespace. (`BusFormViewModel` is already `ViewModels.Bus`.)
- XAML today has **no `{Binding}`** (named `SfTextBoxExt` + click handlers). Wave 1 replaces that with VM bindings and `Command` on ButtonAdv.
- Align VM property names with the dialog (`LicenseNumber` vs current `LicensePlate`; add `Year` which the VM currently lacks).
- **KEEP** this dialog even though its only caller (`BusManagementViewModel`) is never constructed. Do not delete it as an orphan. Live add-bus path today is `BusForm` (`MainWindow.AddBus_Click`, QuickActions).

### 3. FuelDialog becomes a real ViewModel later

- Wave 2 may add/confirm TwoWay bindings first while `DataContext = this` remains.
- Wave 3 extracts a dedicated `FuelDialogViewModel`. Do **not** extract the VM in Wave 2.

### 4. VehicleForm hosts BusForm (add/edit)

- `VehicleForm` MUST host **BusForm**, not `VehicleManagementView`.
- Fleet grid stays on `VehicleManagementView` / `VehiclesView` / the MainWindow Buses window.
- `BusForm` is a `ChromelessWindow`. Wave 3 may extract a shared user-control surface or have `VehicleForm` show/host BusForm without nesting two windows incorrectly.
- No `new VehicleForm(` callers today; still retarget the host so leftover/future navigation cannot open the fleet grid as a “form.”

### 5. ComboBoxAdv pattern

- Required: `ItemsSource` + `SelectedItem` (+ `DisplayMemberPath` for object items).
- Ban: WPF `ComboBoxItem` children and `SelectedValuePath=Content`.
- **DriverForm is already compliant on `master` (PR #48).** Do **not** rewrite DriverForm ComboBoxAdv or its ViewModel option lists in 009.
- Do **not** “fix” MapView / MainWindow `ComboBoxItemAdv` theme or tracking-interval pickers (Maps/007 + shell out of scope).
- StudentForm `SelectedValuePath="RouteName"` is allowed (it is not Content). Nested `ComboBoxAdv.ItemsSource` PriorityBinding on Grade/State SHOULD move to VM lists.

### 6. Shared FieldInputStyle

- `Height=40`, `Padding=8,5`. Match PR #48 DriverForm `FieldInputStyle` and clerk-path `Height="40"` asserts.
- Do **not** change the shared standard to Height 32 unless the user explicitly overrides.
- Wave 5 applies the shared Height **40** style; earlier waves MUST NOT introduce Height 32 as the competing standard.

## Shared constraints (every implementation wave)

- Syncfusion-only UI. Never replace ComboBoxAdv / SfMaskedEdit / ButtonAdv / SfDatePicker / DoubleTextBox with stock WPF.
- Branch from `master` as `feature/<wave-name>`. One wave per PR.
- Mac build: `dotnet build BusBuddy.WPF/BusBuddy.WPF.csproj -c Release -p:EnableWindowsTargeting=true`
- Do not start Wave N+1 until that wave’s acceptance checks pass on Mac build **and** the named dialogs have been clicked in the Windows VM.
- Do not run `specify init --here --force`.
- Do not touch Route Assignment drag-drop or Maps/007.

## User Scenarios & Testing _(mandatory)_

### User Story 1 - Dead route surfaces gone (Priority: P1)

As a transportation clerk, I only see route editing on the live Route Assignment / Route Management path. I never open a leftover Route edit or Route builder UserControl that does not persist.

**Why this priority**: Agents keep treating those files as incomplete features. Deleting them prevents wasted rewrites and false “done” claims.

**Independent Test**: After Wave 1, the RouteEditDialog / RouteForm files are absent; `RouteEditDialog_SaveCommandIsImplemented` is gone or rewritten so it does not read deleted files; Mac Release WPF project builds; Windows VM has no menu or button that opens those surfaces.

**Acceptance Scenarios**:

1. **Given** the Wave 1 branch, **When** a repo search for `RouteEditDialog` / `RouteForm` in `*.cs` / `*.xaml` runs, **Then** no WPF view, VM, or live navigation remains (tests updated).
2. **Given** Route Assignment is open, **When** the clerk edits stops, **Then** `RouteStopsEditor` still appears (not deleted with RouteForm).

---

### User Story 2 - Bus add/edit persists through a ViewModel (Priority: P1)

As a clerk adding or editing a bus, fields I type (number, make, model, year, seating, license) are bound to a ViewModel and saved — not copied from named controls in click handlers. Opening Vehicle Form shows the same add/edit surface as BusForm, not the fleet grid.

**Why this priority**: Unbound BusEditDialog and VehicleForm-as-grid are the main MVVM gaps on the fleet path.

**Independent Test**: BusEditDialog XAML uses `{Binding}` to `BusEditDialogViewModel` in namespace `ViewModels.Bus`. VehicleForm content is BusForm (add/edit), not VehicleManagementView. Fleet grid still opens from Buses / VehiclesView.

**Acceptance Scenarios**:

1. **Given** BusEditDialog is shown with an existing bus, **When** the clerk changes seating capacity and Save, **Then** the ViewModel (not code-behind TextBox names) supplies the saved values.
2. **Given** VehicleForm is opened, **When** the window loads, **Then** the clerk sees BusForm add/edit fields, not the fleet SfDataGrid.
3. **Given** MainWindow Buses (or VehiclesView), **When** the clerk opens fleet management, **Then** VehicleManagementView still lists buses.

---

### User Story 3 - Fuel record fields round-trip (Priority: P2)

As a clerk recording a fuel fill, date, location, bus, odometer, type, gallons, price, total, and notes update the record I save.

**Why this priority**: Fuel is on the clerk path (hop 6) and already live, but the Window is its own DataContext.

**Independent Test**: Wave 2 — every fuel input is TwoWay (or documented equivalent) while `DataContext = this` may remain. Wave 3 — a `FuelDialogViewModel` is the DataContext; FuelManagementViewModel constructs the dialog with that VM.

**Acceptance Scenarios**:

1. **Given** Fuel → Add, **When** the clerk sets gallons and price, **Then** total and MPG reflect the values before Save.
2. **Given** Wave 3 complete, **When** inspecting FuelDialog, **Then** DataContext is not the Window (`DataContext = this` is gone).

---

### User Story 4 - Dropdowns and field chrome are consistent (Priority: P2)

As a clerk, status, school, bus, and similar dropdowns show a list and keep the item I pick. Inputs across forms share one field style (height **40**, padding 8,5).

**Why this priority**: Wrong ComboBoxAdv patterns silently drop SelectedItem; mixed heights make forms feel unfinished.

**Independent Test**: No Views XAML under BusBuddy.WPF uses WPF ComboBoxItem children or `SelectedValuePath=Content` (MapView / MainWindow ComboBoxItemAdv exempt). Shared FieldInputStyle matches the locked Height **40** setters. Windows VM: pick a status on DriverForm (already wired) and a school on StudentForm and Save.

**Acceptance Scenarios**:

1. **Given** DriverForm (already on `master`), **When** the clerk selects Status from ComboBoxAdv, **Then** Save persists that string (ItemsSource + SelectedItem). Do not re-implement this in 009.
2. **Given** Wave 5, **When** opening DriverForm / StudentForm / FuelDialog, **Then** text inputs use Height **40** (shared style), not a new Height 32 standard.

---

### Edge Cases

- BusEditDialog remains unreachable from UI if fleet CRUD never constructs `BusManagementViewModel` — still KEEP and bind; do not delete. VM smoke may note “unreachable until fleet wires it.”
- VehicleForm has no `new VehicleForm(` callers — still retarget host.
- Nested ChromelessWindow (VehicleForm hosting BusForm as another window) is invalid; extract a user control or replace VehicleForm content without two chrome windows.
- StudentForm AM/PM `SelectedValuePath="RouteName"` is not the banned Content pattern.
- MapView / MainWindow `ComboBoxItemAdv` children are out of scope (do not rewrite as ItemsSource in this feature).
- RouteStopsEditor must survive RouteForm deletion.
- Do not rewrite DriverForm to “finish” 009 ComboBox work; it is done on `master`.

## Requirements _(mandatory)_

### Functional Requirements

- **FR-001**: System MUST delete `RouteEditDialog` (XAML + code-behind), `RouteEditDialogViewModel`, and `RouteForm.xaml`, and MUST update `MainWindowClerkPathTests.RouteEditDialog_SaveCommandIsImplemented` (and any other tests that still read those files).
- **FR-002**: System MUST keep `RouteStopsEditor` and MUST NOT change Route Assignment drag-drop or Maps/007 as part of this feature.
- **FR-003**: `BusEditDialog` MUST set DataContext to `BusEditDialogViewModel` and bind fields TwoWay; Save/Cancel MUST use VM commands (not named-control click copy).
- **FR-004**: Types under `BusBuddy.WPF/ViewModels/Bus/` MUST use namespace `BusBuddy.WPF.ViewModels.Bus` (not `ViewModels.BusManagement`).
- **FR-005**: Wave 2 MAY complete TwoWay bindings on `FuelDialog` while the Window remains DataContext; Wave 3 MUST introduce `FuelDialogViewModel` and stop `DataContext = this`.
- **FR-006**: `VehicleForm` MUST host BusForm add/edit content, not `VehicleManagementView`. Fleet listing MUST remain on VehicleManagementView / VehiclesView / MainWindow Buses.
- **FR-007**: ComboBoxAdv on in-scope forms MUST use ItemsSource + SelectedItem (DisplayMemberPath for objects). WPF ComboBoxItem children and `SelectedValuePath=Content` are forbidden on those forms. DriverForm already satisfies this on `master`.
- **FR-008**: Shared `FieldInputStyle` MUST set Height=40, Padding=8,5 to match PR #48 and clerk-path tests. Wave 5 applies it; earlier waves MUST NOT standardize on Height=32.
- **FR-009**: UI MUST remain Syncfusion-only for ComboBoxAdv, SfMaskedEdit, ButtonAdv, SfDatePicker, and DoubleTextBox (no stock WPF replacements).
- **FR-010**: Each implementation wave MUST be a separate PR; Wave N+1 MUST NOT start until Mac Release build of `BusBuddy.WPF` succeeds and the Windows VM smoke clicks for that wave are done.

### Key Entities

- **Keep-and-bind dialog**: BusEditDialog, FuelDialog, DriverForm, BusForm — live or reserved surfaces that agents must not delete.
- **Delete-orphan**: RouteEditDialog, RouteForm, RouteEditDialogViewModel — no live constructor/navigation.
- **Host window**: VehicleForm — add/edit host, not fleet grid.
- **Fleet grid**: VehicleManagementView / VehiclesView — listing only.

## Success Criteria _(mandatory)_

### Measurable Outcomes

- **SC-001**: After Wave 1, a clerk cannot open RouteEditDialog or RouteForm from the running app (files gone; clerk-path test updated; no navigation).
- **SC-002**: After Wave 1, BusEditDialog fields round-trip through a ViewModel; namespace `ViewModels.BusManagement` is gone from `ViewModels/Bus/`.
- **SC-003**: After Wave 3, FuelDialog DataContext is a ViewModel class (not the Window), and VehicleForm shows add/edit bus fields rather than the fleet grid.
- **SC-004**: After Wave 2/5, in-scope ComboBoxAdv instances use ItemsSource + SelectedItem; compliance scan finds zero `SelectedValuePath=Content` and zero WPF ComboBoxItem children in Views (exempt: MapView / MainWindow ComboBoxItemAdv). DriverForm already meets this on `master`.
- **SC-005**: After Wave 5, shared FieldInputStyle is Height **40** / Padding 8,5. DriverForm and StudentForm keep Height 40 via the shared style (not Height 32).
- **SC-006**: Each wave’s Mac `dotnet build BusBuddy.WPF/BusBuddy.WPF.csproj -c Release -p:EnableWindowsTargeting=true` succeeds before the next wave starts.

## Assumptions

- PR #48 is **on `master`**. DriverForm ComboBoxAdv is already ItemsSource + SelectedItem. `SchoolDestinationForm` and `MainWindowClerkPathTests` exist. 009 MUST NOT rewrite DriverForm.
- `BusManagementViewModel` may stay unused after the namespace move; wiring it into DI/navigation is out of scope unless a later wave needs a reachable BusEditDialog for VM smoke.
- SchoolDestinationForm PushFields-on-Save remains an allowed pattern in Wave 4 (do not force live TwoWay if that path is intentional).
- QuickActionsDialog is actions-only — Wave 4 checkbox is “no data-entry change.”
- Windows VM smoke is human click-through (`./run-wpf.sh`); Mac cannot run WPF testhost.

## Out of scope

- Route Assignment drag-drop, Maps/007, Earth Engine.
- Replacing Syncfusion inputs with stock WPF.
- Rewriting DriverForm ComboBoxAdv / option lists (done on `master`).
- Deleting BusEditDialog, FuelDialog, DriverForm, RouteStopsEditor, or VehicleManagementView (grid).
- `specify init --here --force`.
- Rewriting MainWindow theme ComboBoxAdv or MapView interval ComboBoxItemAdv.
- Wave 1 XAML/C# in the Wave 0 PR.
