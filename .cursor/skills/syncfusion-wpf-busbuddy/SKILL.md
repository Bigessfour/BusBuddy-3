---
name: syncfusion-wpf-busbuddy
description: Applies Syncfusion WPF patterns inside BusBuddy-3. Use when editing BusBuddy WPF XAML, Syncfusion controls, SfSkinManager theming, or MVVM views. Loads official Syncfusion component skills from .agents/skills and enforces BusBuddy-specific namespaces, themes, and protected implementations.
---

# Syncfusion WPF — BusBuddy-3

## Layering

1. **Official Syncfusion skills** — `.agents/skills/syncfusion-wpf-*` (installed via [Syncfusion WPF Agent Skills](https://help.syncfusion.com/wpf/skills/component-skills)). Read the matching component skill before generating or changing control markup.
2. **This skill** — BusBuddy-only rules that override generic Syncfusion defaults.
3. **Repo docs** — see [reference.md](reference.md) for file paths and package map.

Vendor skills are **gitignored** (`.agents/skills/`). Install or update after clone:

```bash
.github/scripts/setup-syncfusion-skills.sh          # all 96 components
.github/scripts/setup-syncfusion-skills.sh minimal  # interactive subset
npx skills list
npx skills update
```

## BusBuddy packages in use

Version: `$(SyncfusionVersion)` in `Directory.Build.props` (currently **33.x**). Match NuGet references in `BusBuddy.WPF/BusBuddy.WPF.csproj` — do not add packages the project does not already reference unless explicitly requested.

| Control / area | NuGet | Official skill |
|----------------|-------|----------------|
| SfDataGrid | Syncfusion.SfGrid.WPF | `syncfusion-wpf-datagrid` |
| SfChart | Syncfusion.SfChart.WPF | `syncfusion-wpf-charts` |
| SfTextBoxExt, inputs | Syncfusion.SfInput.WPF | `syncfusion-wpf-textboxext`, `syncfusion-wpf-combobox`, etc. |
| ButtonAdv | Syncfusion.Tools.WPF | `syncfusion-wpf-button` |
| SfBusyIndicator | Syncfusion.SfBusyIndicator.WPF | `syncfusion-wpf-busy-indicator` |
| Navigation drawer | Syncfusion.SfNavigationDrawer.WPF | `syncfusion-wpf-navigation-drawer` |
| SfTreeView | Syncfusion.SfTreeView.WPF | `syncfusion-wpf-treeview` |
| SfScheduler | Syncfusion.SfScheduler.WPF | `syncfusion-wpf-scheduler` |
| SfAccordion | Syncfusion.SfAccordion.WPF | `syncfusion-wpf-accordion` |
| Themes | FluentDark / FluentLight | `syncfusion-wpf-skin-manager` |

## Mandatory XAML patterns

**Primary namespace** (most controls):

```xml
xmlns:syncfusion="http://schemas.syncfusion.com/wpf"
```

Add CLR namespaces only when the control requires them (see `BusBuddy.WPF/Resources/SyncfusionV30_Validated_ResourceDictionary.xaml` header comments).

**Resource dictionary** in views:

```xml
<ResourceDictionary Source="/BusBuddy.WPF;component/Resources/SyncfusionV30_Validated_ResourceDictionary.xaml"/>
```

## Theming — do not break

Theming is **global** in `BusBuddy.WPF/App.xaml.cs`:

- `SfSkinManager.ApplyStylesOnApplication = true`
- `SfSkinManager.ApplicationTheme = new Theme("FluentDark")` (or FluentLight)

**Do not** manually merge Syncfusion theme ResourceDictionaries in individual views. Custom brushes live in `BusBuddy.WPF/Resources/Themes/` and `SyncfusionV30_Validated_ResourceDictionary.xaml` (colors/brushes only).

## SfDataGrid standard (preserve existing views)

```xml
<syncfusion:SfDataGrid ItemsSource="{Binding Items}"
                       SelectedItem="{Binding SelectedItem, Mode=TwoWay}"
                       AutoGenerateColumns="False"
                       AllowSorting="True"
                       AllowFiltering="True"
                       SelectionMode="Single"
                       GridLinesVisibility="Both">
    <syncfusion:SfDataGrid.Columns>
        <syncfusion:GridTextColumn HeaderText="Name" MappingName="Name" Width="150"/>
    </syncfusion:SfDataGrid.Columns>
</syncfusion:SfDataGrid>
```

Use `GridTextColumn`, `GridDateTimeColumn`, etc. — not WPF `DataGrid` columns. No invented properties; verify against official API or the installed component skill.

## Protected implementations

Do not regress working grids or revert to `DataGrid`:

- `BusBuddy.WPF/Views/Student/StudentsView.xaml`
- `BusBuddy.WPF/Views/Fuel/FuelReconciliationDialog.xaml`
- `BusBuddy.WPF/Views/Bus/VehicleManagementView.xaml`

Prefer matching patterns in these files when touching similar views.

## Verification checklist

- [ ] Read the relevant `.agents/skills/syncfusion-wpf-<component>/SKILL.md` first
- [ ] Property exists in Syncfusion WPF API (no hallucinated props)
- [ ] MVVM bindings use `MappingName` on grid columns, not `Binding` on column types that require MappingName
- [ ] Theme stays on SfSkinManager; no duplicate theme merges
- [ ] Build: `dotnet build BusBuddy.sln -c Release -p:EnableWindowsTargeting=true`

## Anti-patterns

- Replacing `SfDataGrid` with WPF `DataGrid`
- Setting `ShowCheckBox` on SfDataGrid (use `GridCheckBoxColumn`)
- Hardcoding Syncfusion 30.x API if repo is on 33.x — check `Directory.Build.props`
- Mixing patterns from other projects (AICO, FlowCheck, etc.)
