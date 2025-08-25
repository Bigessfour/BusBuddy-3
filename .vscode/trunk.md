Trunk configuration for BusBuddy

This workspace enforces Trunk for repo-wide consistency and linting.

Key settings (in .vscode/settings.json):

- trunk.enabled: true
- trunk.autoRun: true (runs on save for targeted languages)
- trunk.languages: [csharp, powershell, xml, sql]
- trunk.linters:
    - PSScriptAnalyzer: true (PowerShell linting)
    - Roslynator: true (C# analyzers & refactorings)
    - XamlStyler: true (XAML formatting)

How to use locally:

- Install recommended extensions (VS Code will prompt from .vscode/extensions.json)
- Trunk runs automatically on save. To run manually, use the Trunk command palette actions (e.g., "Trunk: Check").

Repository policies:

- Trunk is the canonical formatter/linter. Do not add alternative formatters that conflict with Trunk in .vscode/extensions.json.
- For any code change affecting XAML/SF controls, follow Syncfusion official docs and ensure Trunk checks pass before committing.

CI/Pre-commit:

- CI should run Trunk check as part of bb-quality-check or bb-anti-regression tasks.

If issues appear, run:

- Trunk: Check -> shows lint/format results
- Trunk: Fix -> attempts to auto-fix problems

For PowerShell-specific linting, PSScriptAnalyzer settings are in `PSScriptAnalyzerSettings.psd1` at repository root.
