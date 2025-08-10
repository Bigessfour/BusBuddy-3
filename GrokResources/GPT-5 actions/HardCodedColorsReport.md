# Hard-Coded Colors Replacement Report

Date: 2025-08-09

Purpose: Replace hard-coded colors in XAML views with theme-based DynamicResource bindings to support Syncfusion SfSkinManager theming.

## Summary

- Updated views: 
  - Views/Vehicle/VehicleForm.xaml
  - Views/Route/RouteManagementView.xaml
  - Views/Reports/ReportsView.xaml
  - Views/Route/RouteAssignmentView.xaml
  - Views/GoogleEarth/GoogleEarthView.xaml (marker template only)
- Added Serilog load logging to:
  - VehicleForm.xaml.cs
  - RouteManagementView.xaml.cs
  - ReportsView.xaml.cs
  - RouteAssignmentView.xaml.cs

## Replacements Made

### VehicleForm.xaml
- Header background: #2D2D30 → {DynamicResource BusBuddy.Brush.Surface.Light}
- Header title Foreground: White → {DynamicResource BusBuddy.Brush.Text.Primary}
- Search box: Background #3C3C3C, Foreground White, BorderBrush #555555 → {DynamicResource ContentBackgroundBrush}, {DynamicResource PrimaryTextBrush}, {DynamicResource TextBoxBorderBrush}
- Dashboard tiles: #323130/#2ECC71/#FF8C00 and Whites → BusBuddy.Brush.Surface.Border / BusBuddy.Brush.FleetGreen / BusBuddy.Brush.SafetyOrange with BusBuddy text brushes
- Left/right panel backgrounds: #252526 → {DynamicResource BusBuddy.Brush.Panel.Content}
- Buttons: New Vehicle #0078D4, Refresh #5A5A5A → BusBuddy.Brush.Primary / ButtonBackgroundBrush
- GridSplitter background: #323130 → BusBuddy.Brush.Surface.Border
- Form labels Foreground #CCCCCC → BusBuddy.Brush.Text.Secondary
- Inputs Background #3C3C3C, Foreground White, BorderBrush #555555 → ContentBackgroundBrush / PrimaryTextBrush / TextBoxBorderBrush
- Save/Delete: #2ECC71/#E74C3C → BusBuddy.Brush.FleetGreen / BusBuddy.Brush.Semantic.Error
- Utilization panel: #1E1E1E → BusBuddy.Brush.Panel.Background; value Foreground #0078D4 → BusBuddy.Brush.Primary; caption Foreground #CCCCCC → BusBuddy.Brush.Text.Secondary
- Status bar: Background #323130 → BusBuddy.Brush.Surface.Border; text Foreground #CCCCCC → BusBuddy.Brush.Text.Secondary; spinner Foreground #0078D4 → BusBuddy.Brush.Primary

### RouteManagementView.xaml
- Root background FluentDarkBackgroundBrush → PrimaryBackgroundBrush
- Header: #007ACC and White → BusBuddy.Brush.Primary and BusBuddy.Brush.Text.Primary
- Toolbar background: #F5F5F5 → BusBuddy.Brush.Surface.LightMode.Accent
- Export/Print buttons: literal HEX → semantic BusBuddy brushes
- Status bar: #E9ECEF and #6C757D → LightMode.Surface and Text.Secondary; metrics colors mapped to BusBuddy brushes

### ReportsView.xaml
- GroupBox background/border: hard-coded → LightMode.Accent/LightMode.Border
- Header bar: #007ACC/White → BusBuddy.Brush.Primary/Text.Primary
- Section descriptions: #6C757D → Text.Secondary
- All buttons mapped to BusBuddy semantic/primary brushes
- Status bar: background and text → LightMode.Surface and Text.Secondary; last generated primary color → BusBuddy.Brush.Primary; warning panel border → Semantic.Warning

### RouteAssignmentView.xaml
- Header emoji/text Foreground White → ButtonForegroundBrush
- New Route button: #007ACC/White → ButtonBackgroundBrush/ButtonForegroundBrush

### GoogleEarthView.xaml
- Marker Fill/Stroke: Tomato/White → BusBuddy.Brush.Semantic.Error / BusBuddy.Brush.Text.Primary
- Marker label Foreground stays dynamic; background semi-transparent kept as-is

## Logging
- OnLoaded Serilog info added to the listed code-behind files to confirm theme resource usage.

## Items Deferred
- Many remaining hard-coded colors exist across other views (e.g., DriversView, DriverManagementView, FuelReconciliationDialog). Recommend iterating the same replacement pattern.
- Some semi-transparent ARGB backgrounds (e.g., #AA2B2B2B overlays) kept for legibility—consider themed equivalents later.

## Next Steps
- Continue scanning BusBuddy.WPF/Views for remaining matches and replace.
- Validate both FluentDark and FluentLight theme switching at runtime.
- Consider a build-time analyzer or script to flag future hard-coded colors.
