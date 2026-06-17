# BusBuddy Syncfusion reference paths

## Local documentation

| Topic | Path |
|-------|------|
| Full API reference | `BusBuddy.WPF/Documentation/SYNCFUSION_API_REFERENCE.md` |
| Development standards | `BusBuddy.WPF/Documentation/BUSBUDDY_DEVELOPMENT_STANDARDS.md` |
| Copilot / agent rules | `.github/copilot-instructions.md` (Syncfusion protection section) |
| Brand colors & brushes | `BusBuddy.WPF/Resources/SyncfusionV30_Validated_ResourceDictionary.xaml` |
| Fluent theme overrides | `BusBuddy.WPF/Resources/Themes/FluentLightTheme.xaml`, `CustomBrushes.xaml` |
| Theme bootstrap | `BusBuddy.WPF/App.xaml.cs` (`SfSkinManager`) |
| Examples | `Documentation/Reference/Syncfusion-Examples.md` |
| Theming audit | `Documentation/Theming/Theming-Audit-Checklist.md` |

## External

- [Syncfusion WPF Agent Skills install guide](https://help.syncfusion.com/wpf/skills/component-skills)
- [WPF API reference](https://help.syncfusion.com/cr/wpf/Syncfusion.html)
- [Skin Manager / themes](https://help.syncfusion.com/wpf/themes/skin-manager)

## Install (local only — not in GitHub)

`.agents/skills/` is **gitignored**. After clone:

```bash
.github/scripts/setup-syncfusion-skills.sh          # all 96 components
.github/scripts/setup-syncfusion-skills.sh minimal  # interactive subset
```

Recommended minimum for BusBuddy: datagrid, charts, button, busy-indicator, navigation-drawer, treeview, scheduler, accordion, textboxext, skin-manager.

This directory (`.cursor/skills/syncfusion-wpf-busbuddy/`) **is committed** — it layers BusBuddy rules on top of the vendor skills.
