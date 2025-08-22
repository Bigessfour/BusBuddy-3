# BusBuddy WPF Theming Audit — Syncfusion SfSkinManager (FluentDark/FluentLight)

(Migrated from former /docs directory during documentation unification — August 2025)

This checklist verifies that every view (Window/UserControl) correctly integrates Syncfusion WPF 30.1.42 theming via `SfSkinManager` and BusBuddy brand dictionaries.

Documentation-first references (use these only):

- SfSkinManager API: https://help.syncfusion.com/cr/wpf/Syncfusion.SfSkinManager.html
- WPF Themes (Getting Started): https://help.syncfusion.com/wpf/themes/getting-started
- Theme markup extension: https://help.syncfusion.com/cr/wpf/Syncfusion.Theme.html

(Original content unchanged below)

---

## What to check per view

1. DynamicResource for theme-aware visuals

- All colors/brushes must use DynamicResource, not StaticResource or literal hex values
    - Examples: `BusBuddy.Brush.Primary`, `BusBuddy.Brush.Surface`, `ButtonBackgroundBrush`, `ButtonForegroundBrush`, `ButtonBorderBrush`
- No hardcoded `#RRGGBB` for backgrounds/foregrounds/borders in themed UI
- If the view defines local styles/templates, ensure setters bind with DynamicResource

2. Syncfusion namespace and controls

- Root XAML declares: `xmlns:syncfusion="http://schemas.syncfusion.com/wpf"`
- Use Syncfusion controls consistently (no regressions):
    - High-usage: `ButtonAdv`, `SfDataGrid`, `SfTextBoxExt`, `ComboBoxAdv`, `TabControlExt`
    - Specialized: `SfScheduler`, `SfChart` (axes/legend brushes also theme-aware)
- Ensure no standard `DataGrid`, `Button`, or `TextBox` are used where Syncfusion equivalents exist

3. Theme application/inheritance

- Prefer view inheritance from application theme:
    - `syncfusion:SfSkinManager.ApplyStylesOnApplication="True"`
- For intentional overrides, set:
    - `syncfusion:SfSkinManager.Theme="{syncfusion:Theme FluentDark}"` (or FluentLight) — use sparingly
- Confirm `SkinManagerService` applies theme at app startup and supports switching at runtime

4. Runtime theme switching (no visual glitches)

- DynamicResource only for theme brushes — never StaticResource for themable values
- Validate that all visible states update on switch (dark ↔ light):
    - ButtonAdv states, grid header/row/selection, text inputs, tabs, schedulers, charts
- Typical glitch causes: StaticResource, hex color literals, or missing keys in a theme dictionary

5. Fallback behavior (default to FluentLight)

- `SkinManagerService` must guard theme application (try FluentDark; fallback FluentLight on error)
- Views should not assume a theme name — accept inherited/assigned theme
- Any per-view override should still follow service-level fallback semantics

6. Coverage of control set and style keys

- Ensure both FluentDark and FluentLight define the same keys used by views
- Confirm high-usage and specialized controls are covered by implicit styles or explicit DynamicResource set
- For `SfDataGrid` column/cell styles or templates, ensure brush keys are DynamicResource

7. Resource dictionaries

- App.xaml or view-level resources must merge the brand and theme dictionaries:
    - `SyncfusionV30_Validated_ResourceDictionary` (brand palette)
    - `FluentDarkTheme` and `FluentLightTheme` (control styles), with matching keys
- Verify keys exist for all referenced resources; missing keys will break runtime switching

8. Theme-safe patterns only

- No direct ARGB assignments in code-behind for themable properties
- No fixed brush values in implicit styles inside views — use theme keys via DynamicResource
- Avoid multiple per-control theme overrides; prefer inherited app theme to reduce mismatch

9. Minimal QA per view (manual)

- Run app, toggle theme via `SkinManagerService`:
    - Check backgrounds, foregrounds, border/hover/pressed/disabled/selection/validation visuals
    - Watch for any element failing to update — replace StaticResource/hardcoded colors with DynamicResource

10. Documentation traceability

- In comments near theme application and dictionaries, include doc links:
    - `SfSkinManager` API and Theme markup extension (links above)

## Pass criteria

- View declares `xmlns:syncfusion` and only uses Syncfusion controls for themed UI
- All themable brushes use `DynamicResource`
- Inherits app theme via `ApplyStylesOnApplication="True"` or uses a documented override
- No standard `DataGrid`/`Button`/`TextBox` where Syncfusion equivalents exist
- Runtime switch shows no glitches; fallback to FluentLight works

## Automated assist

- Use `PowerShell/Validation/Audit-Themes.ps1` to generate a quick report on XAML conformance
