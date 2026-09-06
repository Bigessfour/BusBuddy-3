---
name: wpf-page-vertical-audit
description: End-to-end audit of a BusBuddy WPF page (Syncfusion controls, MVVM bindings, EF persistence, Postgres constraints). Use when evaluating a view, debugging save/runtime errors, validating new forms, or running the binding audit script.
---

# WPF Page Vertical Audit

## Prerequisites

1. Read [syncfusion-wpf-busbuddy/SKILL.md](../syncfusion-wpf-busbuddy/SKILL.md)
2. `busbuddy-rag` → `search_repo_context` for the page name
3. Syncfusion MCP: `syncfusion-wpf-assistant` → `search_docs` per control type

## Inputs

| Input             | Required | Example                                                                    |
| ----------------- | -------- | -------------------------------------------------------------------------- |
| Page path or name | Yes      | `StudentForm`, `Views/Student/StudentForm.xaml`                            |
| Symptom           | No       | "Save fails on Add Student"                                                |
| Log hint          | No       | Windows VM `C:\dev\BusBuddy-3\BusBuddy.WPF\bin\Debug\net9.0-windows\logs\` |

## Workflow

Copy this checklist and track progress:

```
Audit progress:
- [ ] Phase 0 — Resolve view triad (xaml, code-behind, ViewModel)
- [ ] Phase 1 — WPF / MVVM conventions
- [ ] Phase 2 — Syncfusion inventory + MCP verification
- [ ] Phase 3 — View ↔ ViewModel binding matrix
- [ ] Phase 4 — Save/command pipeline to EF
- [ ] Phase 5 — Entity ↔ DbContext ↔ migrations
- [ ] Phase 6 — Report (P0–P3) + test plan
```

### Phase 0 — Page intake

Resolve and record:

- View: `BusBuddy.WPF/Views/**/{Page}.xaml`
- Code-behind: `.xaml.cs`
- ViewModel: `BusBuddy.WPF/ViewModels/**/{Page}ViewModel.cs`
- Entity + service + `BusBuddyDbContext` configuration
- Field-key constants (e.g. `StudentFormFields`)

### Phase 1 — WPF / Microsoft MVVM

- [ ] `xmlns:syncfusion="http://schemas.syncfusion.com/wpf"`
- [ ] Merged `SyncfusionV30_Validated_ResourceDictionary.xaml` (no per-view theme merges)
- [ ] `DynamicResource` for theme brushes ([Theming-Audit-Checklist.md](../../../Documentation/Theming/Theming-Audit-Checklist.md))
- [ ] `AutomationProperties.Name` on actionable controls
- [ ] Business logic in ViewModel, not code-behind (chrome/focus/diagnostics only)
- [ ] Commands: `AsyncRelayCommand` / `RelayCommand`; `CanSave` aligned with validation
- [ ] App-wide numpad fix registered (`NumpadInputHelper.RegisterApplicationWide()` in `App.OnStartup`)

### Phase 2 — Syncfusion inventory + MCP

Run binding audit script (optional but recommended):

```bash
python .github/scripts/audit-wpf-bindings.py BusBuddy.WPF/Views/Student/StudentForm.xaml
```

For each distinct `syncfusion:*` control type:

1. Read matching `.agents/skills/syncfusion-wpf-<component>/SKILL.md`
2. Call MCP `search_docs` with the binding question (e.g. `ComboBoxAdv SelectedItem IsEditable`)
3. Verify properties exist — no hallucinated API

BusBuddy overlay rules: see `syncfusion-wpf-busbuddy` anti-patterns.

### Phase 3 — Binding matrix

Build a table:

| Control | Binding path | Mode | VM property | Entity field | Risk |

Flag:

- Orphan VM properties (no XAML binding)
- Orphan XAML bindings (no VM property)
- `[NotMapped]` aliases (`SpecialInstructions` → `TransportationNotes`)
- `IsEditable="True"` on closed-list combos (route/school validation risk)
- Split validation layers (XAML rules vs VM vs `IStudentService.ValidateStudentAsync`)

### Phase 4 — Save / command pipeline

Trace every gate before `SaveChangesAsync`:

1. Command `CanExecute` / `CanSave`
2. VM validation (`IsValidStudent`, field errors)
3. Service dry-run (`ValidateStudentAsync` before persist)
4. Service persist (`Add*Async` / `Update*Async`)
5. `DatabaseUserMessage` connectivity + FK/unique mapping

### Phase 5 — Entity ↔ database

Cross-check:

- `BusBuddy.Core/Models/{Entity}.cs` annotations
- `BusBuddyDbContext` fluent config
- Latest migrations under `BusBuddy.Core/Migrations/`
- Normalizers (`StudentRecordNormalizer`, etc.)

### Phase 6 — Report template

```markdown
# Page audit: {PageName}

## Executive summary

## Findings

### P0 — Save/runtime

### P1 — Binding / validation mismatch

### P2 — Syncfusion / WPF polish

### P3 — Nice-to-have

## Binding matrix

## Syncfusion MCP notes

## Recommended fixes (ordered)

## Test plan
```

Severity:

- **P0**: Blocks save, data loss, crash
- **P1**: Wrong data bound, service/VM mismatch
- **P2**: Syncfusion/theme/accessibility
- **P3**: Polish, diagnostics, docs

## Log locations (hybrid dev)

| Host                     | Path                                                            |
| ------------------------ | --------------------------------------------------------------- |
| Windows VM (local build) | `C:\dev\BusBuddy-3\BusBuddy.WPF\bin\Debug\net9.0-windows\logs\` |
| Serilog rolling          | `Logs/busbuddy-*.txt` (working directory)                       |
| Runtime UI errors        | `logs/runtime-errors.log`                                       |
| Bootstrap                | `logs/bootstrap-*.txt`                                          |

Search for: `Error saving student`, `Student validation failed`, `FK_Students`, `timestamp with time zone`.

## ComboBox policy (forms)

| Field type          | `IsEditable` | Binding target                                            |
| ------------------- | ------------ | --------------------------------------------------------- |
| Grade, State        | `False`      | Scalar on entity                                          |
| School, Pickup stop | `False`      | VM selector → syncs FK                                    |
| AM/PM Route         | `False`      | `Student.AMRoute` / `PMRoute` from `AvailableRoutes` only |

## Post-audit

- Register surface in `.function-inventory.json`
- Add evidence to `docs/action-items.md`
- Run `/code-review` on P0/P1 fixes before merge
- Run `python -m rag.index` if architecture docs changed

## Reference

- [reference.md](reference.md) — StudentForm validation layers, RAG queries, test plan
- [syncfusion-wpf-busbuddy](../syncfusion-wpf-busbuddy/SKILL.md)
