---
name: wpf-page-audit
description: Audits a designated BusBuddy WPF page end-to-end — WPF/Microsoft layout and MVVM, Syncfusion control inventory and MCP doc verification, View↔ViewModel bindings, and ViewModel↔EF↔database save path. Use when reviewing a view, diagnosing save/runtime errors, or before shipping a form/dialog (e.g. StudentForm add student).
---

# WPF Page Audit (BusBuddy-3)

Systematic, read-first audit of one UI surface from XAML through persistence. Produces a structured report; fixes are a separate step unless the user asks to implement.

## When to use

- User names a page: `StudentForm`, `AnalyticsDashboardView`, `DashboardView`, etc.
- Save/runtime errors on a dialog or form
- Pre-PR review of a WPF surface
- After Syncfusion or EF schema changes

## Inputs

| Input                  | Example                                                                |
| ---------------------- | ---------------------------------------------------------------------- |
| **Page** (required)    | `BusBuddy.WPF/Views/Student/StudentForm.xaml`                          |
| **Mode**               | `full` (default), `bindings-only`, `save-path-only`, `syncfusion-only` |
| **Symptom** (optional) | "Save throws on add student"                                           |

## Layering (read in order)

1. **Repo overlay** — [syncfusion-wpf-busbuddy](../syncfusion-wpf-busbuddy/SKILL.md)
2. **This skill** — audit workflow and report
3. **Surface checklist** — [checklists/](checklists/) when present (e.g. StudentForm)
4. **Report template** — [report-template.md](report-template.md)

## Mandatory pre-reads

Before auditing:

1. `.github/copilot-instructions.md` (Syncfusion-only, Serilog, anti-regression)
2. `.cursor/skills/syncfusion-wpf-busbuddy/SKILL.md`
3. If available: `busbuddy-rag` → `search_repo_context` for the page + entity + save command

Vendor Syncfusion skills: `.agents/skills/syncfusion-wpf-*` (install via `.github/scripts/setup-syncfusion-skills.sh`).

---

## Workflow

Copy this checklist and track progress:

```
Page audit: <PageName>
- [ ] 1. Resolve file graph (View → VM → Model → Service → DbContext)
- [ ] 2. WPF / Microsoft / theme compliance
- [ ] 3. Syncfusion control inventory + MCP per control type
- [ ] 4. View ↔ ViewModel binding matrix
- [ ] 5. ViewModel ↔ EF ↔ database chain
- [ ] 6. Save / command / async / error-mapping trace
- [ ] 7. Tests + manual verification plan
- [ ] 8. Write report (report-template.md)
```

### Step 1 — Resolve the file graph

From the designated XAML, locate and read:

| Artifact             | Typical path pattern                            |
| -------------------- | ----------------------------------------------- |
| View XAML            | `BusBuddy.WPF/Views/**/<Name>.xaml`             |
| Code-behind          | same folder `<Name>.xaml.cs`                    |
| ViewModel            | `BusBuddy.WPF/ViewModels/**/<Name>ViewModel.cs` |
| Model                | `BusBuddy.Core/Models/<Entity>.cs`              |
| Service              | `BusBuddy.Core/Services/*Service.cs`            |
| DbContext            | `BusBuddy.Core/Data/BusBuddyDbContext.cs`       |
| Migrations           | `BusBuddy.Core/Migrations/*` touching the table |
| DI registration      | `BusBuddy.WPF/App.xaml.cs`                      |
| Field keys / helpers | e.g. `StudentFormFields.cs`, `*Normalizer.cs`   |

Note: `ChromelessWindow` hosts (e.g. `StudentForm`) still follow View + ViewModel; code-behind may wire focus/validation only.

### Step 2 — WPF / Microsoft / BusBuddy theme

Check against:

- `BusBuddy.XamlCompliance.Tests` rules (no hardcoded hex foregrounds on themed surfaces; `ButtonAdv` text-only style)
- `SyncfusionV30_Validated_ResourceDictionary.xaml` merged in view
- **No per-view Syncfusion theme dictionaries** (`SfSkinManager` is global in `App.xaml.cs`)
- `AutomationProperties.Name` on interactive controls
- `Label` associated with inputs (or `AutomationProperties.LabeledBy`)
- `Validation.ErrorTemplate` on bound inputs that participate in save validation
- Commands on `ButtonAdv` use `Command` + `Label` (not `Content` alone)
- Loading overlays: `SfBusyIndicator IsBusy="{Binding IsLoading}"` not hardcoded `True`

**Microsoft Learn MCP** (`user-microsoft-learn`): use for WPF MVVM, data binding, validation, and `INotifyPropertyChanged` when unsure.

### Step 3 — Syncfusion inventory + MCP verification

**3a. Inventory controls**

Run from repo root (adjust path):

```bash
rg -o 'syncfusion:[A-Za-z0-9]+' BusBuddy.WPF/Views/<area>/<Page>.xaml | sort -u
```

Or read XAML and list each `syncfusion:*` element with `x:Name` and bound properties.

**3b. Map control → vendor skill → NuGet**

Use the table in `syncfusion-wpf-busbuddy/SKILL.md`. Common BusBuddy controls:

| Control                                                       | Vendor skill                                                 |
| ------------------------------------------------------------- | ------------------------------------------------------------ |
| `SfDataGrid`                                                  | `syncfusion-wpf-datagrid`                                    |
| `SfChart` / `ColumnSeries` / `PieSeries`                      | `syncfusion-wpf-charts`                                      |
| `SfTextBoxExt`, `ComboBoxAdv`, `SfDatePicker`, `SfMaskedEdit` | `syncfusion-wpf-textboxext`, `syncfusion-wpf-combobox`, etc. |
| `ButtonAdv`                                                   | `syncfusion-wpf-button`                                      |
| `SfBusyIndicator`                                             | `syncfusion-wpf-busy-indicator`                              |
| `ChromelessWindow`                                            | `syncfusion-wpf-chromeless-window` (if installed)            |

**3c. MCP verification (required per distinct control type)**

For each control **type** on the page (not every instance), call `syncfusion-wpf-assistant` → `search_docs`:

```
query: "<ControlType> <properties used in XAML> WPF MVVM binding example"
components: "<ControlType>"
```

Verify every non-trivial property in XAML exists in Syncfusion 34.x API. Flag:

- Invented properties (hallucinated API)
- Wrong adornment/axis APIs (e.g. `AdornmentsPosition="OutsideExtended"` on pie — use `LabelPosition="OutsideExtended"` on `PieSeries`)
- `SfDataGrid` columns using `Binding` instead of `MappingName`
- Missing `ChartLegend` / `ShowTooltip` / axis `Header` when data is categorical

**3d. Repo golden references**

Compare against a known-good surface of the same control family:

- Grids: `StudentsView.xaml`, `VehicleManagementView.xaml`
- Charts: `StudentStatisticsPanel.xaml`, `AnalyticsDashboardView.xaml`
- Forms: `SchoolDestinationForm.xaml` (note `PushFieldsToViewModel` pattern if bindings alone leave VM empty)

### Step 4 — View ↔ ViewModel binding matrix

Build a table: **Control** | **Property** | **Binding path** | **Mode** | **VM property exists?** | **Notes**

Rules:

- Every `Text` / `SelectedItem` / `SelectedValue` / `Value` / `IsChecked` / `ItemsSource` / `Command` must resolve on the DataContext type
- Prefer `UpdateSourceTrigger=PropertyChanged` on form fields that gate `CanExecute`
- `ComboBoxAdv` with `DisplayMemberPath`: `SelectedItem` type must match VM property (`Destination` vs `string`)
- Nested paths (`Student.Grade`) require `Student` on VM and `INotifyPropertyChanged` on nested changes — if `Student` is a model object, confirm property changed events propagate or VM replaces `Student` reference
- Commands: `ICommand` / `IAsyncRelayCommand` on VM; `CanExecute` must not silently block save (validate inside `Execute` if needed)
- Orphan VM properties with no XAML binding → dead UI or missing fields on save
- Orphan XAML bindings → runtime binding errors (often swallowed until save)

**Code-behind smell:** if code-behind maps controls by name (`FocusFieldByKey`, `PushFieldsToViewModel`), audit that path explicitly — hybrid forms are a top cause of "UI looks fine, save has nulls".

### Step 5 — ViewModel ↔ EF ↔ database

For each persisted field on the save path:

| VM / Model property | EF mapped? | Column / FK | Max length | Required | Normalizer / default | Migration aligned? |

Read:

- `BusBuddy.Core/Models/<Entity>.cs` (`[Required]`, `[StringLength]`, `[NotMapped]`, `[ForeignKey]`)
- `BusBuddyDbContext.OnModelCreating` for the entity
- Latest migration touching that table
- `*Normalizer.cs` (e.g. `StudentRecordNormalizer` — FK `<= 0` → `null`, UTC dates)

Flag:

- `[NotMapped]` aliases bound in UI but never copied to persisted column (e.g. `EmergencyContactPhone` → `EmergencyPhone`)
- FK ids set to `0` instead of `null`
- `DateTime` Kind issues (Postgres `timestamptz` — must be UTC)
- String length truncation vs validation mismatch
- Service-layer validation stricter than VM (e.g. route name must exist in `Routes` table)
- Unique indexes (student number) not surfaced as field-level errors in VM

### Step 6 — Save / command / async trace

Trace from Save button → `SaveCommand` → service/EF → `SaveChangesAsync`:

1. **CanExecute** — what blocks save before click?
2. **Pre-save guards** — validation, address validation, DB connectivity check
3. **Normalization** — `StudentRecordNormalizer`, phone/zip formatters
4. **Dual paths** — service vs direct `_context` (divergent behavior?)
5. **Exception mapping** — does catch block map DB/validation errors to the right field (`StudentFormFields`)?
6. **Threading** — no `Task.Run` + `ObservableCollection` mutations off UI thread
7. **Post-save** — messenger/events, dialog close, list refresh

For save failures, capture: exception type, Postgres message, inner FK violation, and which layer threw (VM validation vs `ValidateStudentAsync` vs EF).

### Step 7 — Verification

| Check               | Command / action                                                                          |
| ------------------- | ----------------------------------------------------------------------------------------- |
| Build               | `dotnet build BusBuddy.sln -c Release -p:EnableWindowsTargeting=true`                     |
| XAML compliance     | `dotnet test BusBuddy.XamlCompliance.Tests -c Release`                                    |
| View binding smoke  | Add/extend test in `BusBuddy.Tests/WPF/*ViewTests.cs` (string contains critical bindings) |
| Manual (Windows VM) | Open page, fill minimal required fields, save, edit, cancel, validation errors            |

---

## Severity rubric

| Severity | Meaning                                                                           |
| -------- | --------------------------------------------------------------------------------- |
| **P0**   | Save/data loss/runtime crash; wrong FK; binding to missing property               |
| **P1**   | Syncfusion misconfig breaks chart/grid/input; validation bypass; thread violation |
| **P2**   | Missing accessibility, tooltip, legend; theme drift; redundant code-behind        |
| **P3**   | Polish — animation, labels, layout consistency                                    |

---

## Output

Write the report using [report-template.md](report-template.md). Save optional copy under `docs/audits/<page>-<date>.md` only if the user asks.

---

## Enhancement ideas (apply per page)

Beyond defect finding, recommend operational improvements:

1. **Binding contract test** — one `*ViewTests.cs` asserting every `Command=` and critical `Binding=` path exists (pattern: `AnalyticsDashboardViewTests`).
2. **Save path integration test** — in-memory or Testcontainers Postgres with minimal valid entity insert.
3. **Field inventory JSON** — extend `.function-inventory.json` `surfaces` with view/model/service triples.
4. **Golden dialog pattern** — document whether page uses pure MVVM or `PushFieldsToViewModel`; pick one per form.
5. **Diagnostic overlay** — temporary `ValidationStatus` + Serilog context (StudentForm already logs control events; ensure save logs include `StudentId`, FK ids, route names).
6. **Empty-state and error-state UX** — `SfBusyIndicator` + disabled Save + field errors + global banner (StudentForm has all three; verify they do not fight each other).
7. **Combo data freshness** — `AvailableSchools` / `AvailablePickupStops` loaded on open vs stale after admin changes.
8. **Keyboard path** — Tab order, Enter=Save, Esc=Cancel, first-field focus on load.
9. **Post-save refresh** — `WeakReferenceMessenger` / parent list reload (StudentsView after add).
10. **Spec linkage** — if page maps to a `specs/**` story, note coverage gaps in the audit.

---

## Related skills

- `syncfusion-wpf-busbuddy` — repo Syncfusion rules
- `function-inventory` — surface queue maintenance
- `/check-work` — self-verify after fixes
- `review-bugbot` — post-fix diff review
