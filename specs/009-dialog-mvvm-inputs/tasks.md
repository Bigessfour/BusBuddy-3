# Tasks: Dialog MVVM Inputs

**Input**: [spec.md](./spec.md)

**Prerequisites**: spec.md only (Wave 0). No plan.md / research.md for this feature.

**Tests**: Update existing file-text tests when deleting orphans (Wave 1), including `MainWindowClerkPathTests.RouteEditDialog_SaveCommandIsImplemented`. XamlCompliance gates in Wave 5. No new WPF testhost tests (Mac cannot run them).

**Organization**: One checkbox **per file change**, grouped as Wave 1–5 matching the 14-surface forms-data-entry audit. **PR #48 is on `master`.** DriverForm ComboBoxAdv is already ItemsSource + SelectedItem (T013 / T013b complete). SchoolDestinationForm exists. Shared field height is **40**, not 32.

**Gate**: One wave per PR. Branch from `master` as `feature/<wave-name>`. Do **not** start Wave N+1 until:

1. `dotnet build BusBuddy.WPF/BusBuddy.WPF.csproj -c Release -p:EnableWindowsTargeting=true` succeeds on Mac
2. Named dialogs in the Windows VM smoke section for that wave have been clicked

**Shared constraints**: Syncfusion-only UI. Never replace ComboBoxAdv / SfMaskedEdit / ButtonAdv / SfDatePicker / DoubleTextBox with stock WPF. Do not run `specify init --here --force`. Do not touch Route Assignment drag-drop or Maps/007. Do **not** rewrite DriverForm.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no incomplete dependencies)
- **[Story]**: US1–US4 from [spec.md](./spec.md)

---

## Wave 0 — Lock scope (this PR)

**Purpose**: Spec + tasks only. No XAML/C#.

- [x] T000 Create `specs/009-dialog-mvvm-inputs/spec.md` (orphan-search table + six locked decisions)
- [x] T000b Create `specs/009-dialog-mvvm-inputs/tasks.md` (this file)
- [x] T000c Align spec/tasks with `master` after PR #48 (DriverForm already ItemsSource+SelectedItem; FieldInputStyle Height **40**; `SchoolDestinationForm` + `MainWindowClerkPathTests` exist; Wave 1 must update `RouteEditDialog_SaveCommandIsImplemented`)

**Checkpoint**: Spec exists; orphan-delete decision is explicit. No UI rewrite. No DriverForm edits in this PR.

---

## Wave 1 — Orphans + BusEditDialog MVVM (Priority: P1)

**Goal**: Delete dead Route surfaces; bind BusEditDialog to ViewModel; fix `ViewModels.Bus` namespace.

**Branch**: `feature/009-w1-orphan-busedit`

**Independent Test**: Mac Release WPF build. Search finds no RouteEditDialog / RouteForm views. `RouteEditDialog_SaveCommandIsImplemented` no longer reads deleted files. BusEditDialog XAML uses `{Binding}` to `BusEditDialogViewModel`. RouteStopsEditor still in RouteAssignmentView.

### File tasks

- [ ] T001 [US1] Delete `BusBuddy.WPF/Views/Route/RouteEditDialog.xaml`
- [ ] T002 [P] [US1] Delete `BusBuddy.WPF/Views/Route/RouteEditDialog.xaml.cs` (present on `master` after PR #48)
- [ ] T003 [P] [US1] Delete `BusBuddy.WPF/ViewModels/Route/RouteEditDialogViewModel.cs`
- [ ] T004 [P] [US1] Delete `BusBuddy.WPF/Views/Route/RouteForm.xaml` (no code-behind exists; do **not** delete `RouteStopsEditor`)
- [ ] T005 [US1] Update `BusBuddy.Tests/WPF/MainWindowClerkPathTests.RouteEditDialog_SaveCommandIsImplemented` so it does not read deleted files (assert absence, or remove the test). Grep `BusBuddy.Tests` for any other `RouteEditDialog` / `RouteForm` hits.
- [ ] T006 [US2] Bind `BusBuddy.WPF/Views/Bus/BusEditDialog.xaml` to VM (`{Binding}` on SfTextBoxExt / IntegerTextBox; ButtonAdv `Command`; no named-control click copy)
- [ ] T007 [US2] Wire DataContext + close in `BusBuddy.WPF/Views/Bus/BusEditDialog.xaml.cs` (stop LoadBusData from TextBox.Text)
- [ ] T008 [US2] Move + complete `BusBuddy.WPF/ViewModels/Bus/BusEditDialogViewModel.cs` — namespace `BusBuddy.WPF.ViewModels.Bus`; align `LicenseNumber`, add `Year`; hydrate from `Bus`
- [ ] T009 [P] [US2] Namespace only: `BusBuddy.WPF/ViewModels/Bus/BusManagementViewModel.cs` → `BusBuddy.WPF.ViewModels.Bus`
- [ ] T010 [P] [US2] Namespace only: `BusBuddy.WPF/ViewModels/Bus/BusViewModel.cs` → `BusBuddy.WPF.ViewModels.Bus`

**Checkpoint**: Mac build green. Do not start Wave 2 until Windows VM smoke for Wave 1 is checked below. **Do not start Wave 1 in the Wave 0 PR.**

---

## Wave 2 — Fuel TwoWay + ComboBoxAdv pattern (Priority: P2)

**Goal**: Confirm Fuel TwoWay bindings. ComboBoxAdv = ItemsSource + SelectedItem on remaining live forms. **Keep** FuelDialog `DataContext = this` (no VM extract). **Do not rewrite DriverForm.**

**Branch**: `feature/009-w2-fuel-combobox`

**Independent Test**: FuelDialog inputs TwoWay. DriverForm ComboBoxAdv already compliant on `master`. StudentForm Grade/State from VM lists. Do not rewrite MapView / MainWindow ComboBoxItemAdv.

### File tasks

- [ ] T011 [US3] `BusBuddy.WPF/Views/Fuel/FuelDialog.xaml` — confirm TwoWay on SfDatePicker, ComboBoxAdv, DoubleTextBox, Notes; ComboBoxAdv ItemsSource + SelectedItem (+ DisplayMemberPath on Bus)
- [ ] T012 [US3] `BusBuddy.WPF/Views/Fuel/FuelDialog.xaml.cs` — **no** VM extract this wave; `DataContext = this` may remain
- [x] T013 [P] [US4] `BusBuddy.WPF/Views/Driver/DriverForm.xaml` — **complete on `master` (PR #48)**. ItemsSource + SelectedItem already; no WPF `ComboBoxItem`; no `SelectedValuePath=Content`. **Do not rewrite.**
- [x] T013b [P] [US4] `BusBuddy.WPF/ViewModels/Driver/DriverFormViewModel.cs` — **complete on `master` (PR #48)**. StatusOptions, DutyCategoryOptions, VehicleCategoryOptions, MedicalFormTypeOptions, LicenseClassOptions already exist. **Do not rewrite.**
- [ ] T014 [P] [US4] `BusBuddy.WPF/Views/Student/StudentForm.xaml` — Grade/State lists via VM ItemsSource + SelectedItem (move off nested PriorityBinding ItemsSource); AM/PM `SelectedValuePath=RouteName` may stay
- [ ] T015 [P] [US4] `BusBuddy.WPF/ViewModels/Student/StudentFormViewModel.cs` — expose Grade/State (and related) lists for ComboBoxAdv ItemsSource
- [ ] T016 [P] [US4] `BusBuddy.WPF/Views/Activity/ActivityScheduleEditDialog.xaml` — ComboBoxAdv ItemsSource + SelectedItem (+ DisplayMemberPath for objects)
- [ ] T017 [P] [US4] `BusBuddy.WPF/Views/Student/StudentSchoolTransferForm.xaml` — ComboBoxAdv ItemsSource + SelectedItem
- [ ] T018 [P] [US4] `BusBuddy.WPF/Views/Bus/BusForm.xaml` — ComboBoxAdv ItemsSource + SelectedItem
- [ ] T019 [P] [US4] `BusBuddy.WPF/Views/Fuel/FuelReconciliationDialog.xaml` — ComboBoxAdv ItemsSource + SelectedItem only (not a record-entry form)

**Checkpoint**: Mac build green. Fuel Add/Edit + DriverForm + StudentForm clicked in VM before Wave 3.

---

## Wave 3 — FuelDialog VM + VehicleForm hosts BusForm (Priority: P1/P2)

**Goal**: FuelDialog DataContext is a ViewModel. VehicleForm hosts BusForm add/edit, not VehicleManagementView. Do not nest two ChromelessWindows.

**Branch**: `feature/009-w3-fuel-vm-vehicleform`

**Independent Test**: `DataContext = this` gone from FuelDialog. VehicleForm shows bus add/edit fields. Fleet grid still opens from Buses / VehiclesView.

### File tasks

- [ ] T020 [US3] Create `BusBuddy.WPF/ViewModels/Fuel/FuelDialogViewModel.cs` (properties/commands currently on the Window)
- [ ] T021 [US3] `BusBuddy.WPF/Views/Fuel/FuelDialog.xaml.cs` — stop `DataContext = this`; construct/assign FuelDialogViewModel
- [ ] T022 [US3] `BusBuddy.WPF/Views/Fuel/FuelDialog.xaml` — bindings target the new VM (no Window-as-VM)
- [ ] T023 [US3] `BusBuddy.WPF/ViewModels/Fuel/FuelManagementViewModel.cs` — construct FuelDialog with FuelDialogViewModel (pass IBusService / Fuel into the VM, not the Window-as-VM)
- [ ] T024 [US2] `BusBuddy.WPF/Views/Vehicle/VehicleForm.xaml` — host BusForm add/edit; remove `VehicleManagementView` as content
- [ ] T025 [US2] `BusBuddy.WPF/Views/Vehicle/VehicleForm.xaml.cs` — wire host (show BusForm or embedded user-control; no nested second ChromelessWindow)
- [ ] T026 [US2] `BusBuddy.WPF/Views/Bus/BusForm.xaml` — extract shared user-control surface **only if** required to host inside VehicleForm
- [ ] T027 [US2] `BusBuddy.WPF/Views/Bus/BusForm.xaml.cs` — match extract/host change from T026 (skip if T026 unused)

**Checkpoint**: Mac build green. FuelDialog + VehicleForm/BusForm add-edit clicked in VM before Wave 4. Fleet grid still reachable.

---

## Wave 4 — Remaining audit dialogs (Priority: P2)

**Goal**: Remaining 14-surface audit forms get ComboBoxAdv / FieldInputStyle **consume-or-prepare** and MVVM hygiene. Do not delete keep-surfaces. SchoolDestinationForm PushFields-on-Save is allowed.

**Branch**: `feature/009-w4-remaining-dialogs`

**Independent Test**: Each listed form still opens; Syncfusion inputs retained; no stock WPF replacements.

### File tasks

- [ ] T028 [P] [US4] `BusBuddy.WPF/Views/Student/SchoolDestinationForm.xaml` — Syncfusion inputs; FieldInputStyle prepare/consume; do not force live TwoWay if PushFields remains. **Present on `master` after PR #48.**
- [ ] T029 [P] [US4] `BusBuddy.WPF/Views/Student/SchoolDestinationForm.xaml.cs` — keep PushFields-on-Save if still the Save path
- [ ] T030 [P] [US4] `BusBuddy.WPF/ViewModels/Student/SchoolDestinationFormViewModel.cs` — no orphan bind paths introduced
- [ ] T031 [P] [US4] `BusBuddy.WPF/Views/Student/StudentSchoolTransferForm.xaml` — remaining input hygiene (not ComboBox-only from Wave 2)
- [ ] T032 [P] [US4] `BusBuddy.WPF/ViewModels/Student/StudentSchoolTransferViewModel.cs` — keep time-prompt strip / Save executable
- [ ] T033 [P] [US4] `BusBuddy.WPF/Views/Route/RouteStopEditDialog.xaml` — prefer SfTextBoxExt; no Route Assignment drag-drop changes
- [ ] T034 [P] [US4] `BusBuddy.WPF/Views/Route/RouteStopEditDialog.xaml.cs` — keep simple code-behind VM-less dialog unless a small VM is required
- [ ] T035 [P] [US4] `BusBuddy.WPF/Views/Activity/ActivityScheduleEditDialog.xaml` — remaining input hygiene after Wave 2 ComboBoxAdv
- [ ] T036 [P] [US4] `BusBuddy.WPF/Views/Activity/ActivityScheduleEditDialog.xaml.cs` — keep lists/Save wiring
- [ ] T037 [P] [US4] `BusBuddy.WPF/Views/Bus/BusForm.xaml` — FieldInputStyle consume or prepare (Height **40** standard, not 32)
- [ ] T038 [P] [US4] `BusBuddy.WPF/ViewModels/Bus/BusFormViewModel.cs` — no new orphan properties without UI
- [ ] T039 [US4] `BusBuddy.WPF/Views/Student/QuickActionsDialog.xaml` — **no data-entry change** (actions only; checkbox = confirm skip)

**Checkpoint**: Mac build green. Click SchoolDestinationForm, Transfer, Activity schedule edit, BusForm, RouteStop edit if reachable. Do not require Route Assignment drag-drop.

---

## Wave 5 — Shared FieldInputStyle + compliance (Priority: P2)

**Goal**: One shared `FieldInputStyle` (Height=**40**, Padding=8,5). Compliance scan bans `SelectedValuePath=Content` and WPF ComboBoxItem children in Views (exempt MapView / MainWindow ComboBoxItemAdv).

**Branch**: `feature/009-w5-fieldinputstyle`

**Independent Test**: DriverForm consumes shared Height **40** style (it is already 40 locally). StudentForm Height 40 implicit styles align to the shared style. XamlCompliance tests fail if Content SelectedValuePath or WPF ComboBoxItem children appear in in-scope Views.

### File tasks

- [ ] T040 [US4] Extend `BusBuddy.WPF/Resources/SyncfusionStyles.xaml` **or** add `BusBuddy.WPF/Resources/FormFieldStyles.xaml` with shared `FieldInputStyle` (Height=**40**, Padding=8,5)
- [ ] T041 [US4] `BusBuddy.WPF/Views/Driver/DriverForm.xaml` — consume shared FieldInputStyle (local style is already Height=**40**; do **not** change it to 32; do **not** rewrite ComboBoxAdv)
- [ ] T042 [P] [US4] `BusBuddy.WPF/Views/Student/StudentForm.xaml` — consume shared FieldInputStyle where local input styles compete
- [ ] T043 [P] [US4] `BusBuddy.WPF/Views/Fuel/FuelDialog.xaml` — consume shared FieldInputStyle
- [ ] T044 [P] [US4] `BusBuddy.WPF/Views/Bus/BusEditDialog.xaml` — consume shared FieldInputStyle
- [ ] T045 [P] [US4] `BusBuddy.WPF/Views/Bus/BusForm.xaml` — consume shared FieldInputStyle
- [ ] T046 [P] [US4] `BusBuddy.WPF/Views/Student/SchoolDestinationForm.xaml` — consume shared FieldInputStyle
- [ ] T047 [P] [US4] `BusBuddy.WPF/Views/Student/StudentSchoolTransferForm.xaml` — consume shared FieldInputStyle
- [ ] T048 [P] [US4] `BusBuddy.WPF/Views/Activity/ActivityScheduleEditDialog.xaml` — consume shared FieldInputStyle
- [ ] T049 [P] [US4] `BusBuddy.WPF/Views/Route/RouteStopEditDialog.xaml` — consume shared FieldInputStyle
- [ ] T050 [US4] `BusBuddy.XamlCompliance.Tests/XamlThemeComplianceScanner.cs` — ban `SelectedValuePath="Content"` and WPF `<ComboBoxItem` children in Views; exempt MapView / MainWindow `ComboBoxItemAdv`; optional FieldInputStyle Height=**40** gate
- [ ] T051 [US4] `BusBuddy.XamlCompliance.Tests/XamlThemeComplianceTests.cs` — assert scanner findings empty for those rules

**Checkpoint**: Mac build + `dotnet test` XamlCompliance filter green. Windows VM: DriverForm / StudentForm / FuelDialog / BusForm look Height **40**.

---

## Windows VM smoke

Run inside the Windows guest after each wave’s Mac build is green (`./run-wpf.sh` or `.\utm_run_in_vm.ps1`). Check only the rows that apply to the wave just completed. Do **not** require Route Assignment drag-drop or Map dialogs.

### Wave 1

- [ ] DriverForm — Drivers → Add or Edit; window opens, fields visible, Cancel/close (**do not rewrite** the form)
- [ ] Confirm RouteEditDialog / RouteForm **do not appear** (no leftover menu, dock, or button)
- [ ] BusEditDialog — click if a reachable open path exists; otherwise record **unreachable until fleet wires it** (KEEP, do not delete)

### Wave 2

- [ ] FuelDialog — Fuel → Add (and Edit if a row exists); change gallons/price; Save or Cancel
- [ ] DriverForm — change Status ComboBoxAdv; confirm selection sticks (already wired on `master`)
- [ ] StudentForm — change Grade (and School if listed); confirm selection sticks

### Wave 3

- [ ] FuelDialog — Add/Edit still works after VM extract
- [ ] VehicleForm / BusForm add-edit — Add Bus or Vehicle Form shows **bus fields**, not the fleet grid
- [ ] Fleet grid still opens from MainWindow Buses / VehiclesView (`VehicleManagementView`)

### Wave 4

- [ ] SchoolDestinationForm — Students → Add School
- [ ] StudentSchoolTransferForm — Students → School Transfer
- [ ] BusForm — Add Bus
- [ ] ActivityScheduleEditDialog — if reachable from Activity / schedule
- [ ] RouteStopEditDialog — if reachable without using Route Assignment drag-drop

### Wave 5

- [ ] DriverForm — inputs Height **40**, usable padding
- [ ] StudentForm — same chrome
- [ ] FuelDialog — same chrome
- [ ] BusForm / BusEditDialog (if reachable) — same chrome

### Per-wave build (Mac, before VM)

```bash
dotnet build BusBuddy.WPF/BusBuddy.WPF.csproj -c Release -p:EnableWindowsTargeting=true
```

---

## Dependencies

- Wave 0 (spec/tasks) blocks all implementation waves.
- Wave 1 (deletes + BusEditDialog bind) before Wave 3 (VehicleForm host) and Wave 5 (BusEditDialog style).
- Wave 2 (Fuel TwoWay, ComboBoxAdv on remaining forms) before Wave 3 (Fuel VM extract). T013/T013b are already done on `master`.
- Wave 3 before Wave 5 Fuel/VehicleForm style consume.
- Wave 4 may overlap Wave 5 style consume on the same files — prefer Wave 4 hygiene first, then Wave 5 shared style.
- Do not start Wave N+1 until that wave’s Mac build + VM smoke rows are checked.

## Parallel opportunities

- Wave 1: T001–T004 deletes in parallel; T005 test update with the deletes; T009–T010 namespace-only in parallel with T006–T008 only after namespace target is agreed (same PR, T008 owns BusEditDialogViewModel).
- Wave 2: T014–T019 independent XAML files in parallel after T011 Fuel confirm. Skip T013/T013b.
- Wave 4: T028–T039 independent surfaces in parallel.
- Wave 5: T042–T049 consume-style in parallel after T040 shared resource exists.

## Notes

- `RouteStopsEditor` is **live** in RouteAssignmentView — never delete it with RouteForm.
- Height **40** is the 009 standard (PR #48 + clerk-path tests); do not re-introduce Height 32 as the shared style unless the user explicitly overrides.
- Do **not** rewrite DriverForm.
- After the Wave 0 PR merges, later `/speckit-implement` slices use this tasks.md; do not invent new dialogs to “fix.”
