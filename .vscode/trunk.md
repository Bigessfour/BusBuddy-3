# 🚌 Trunk Configuration for BusBuddy

This workspace enforces Trunk for repo-wide consistency and linting, with special handling for GitHub Actions workflows and secret management.

## 🔐 GitHub Actions Secrets Handling

Following [GitHub's official documentation](https://docs.github.com/en/actions/reference/workflows-and-actions/contexts#secrets-context), our workflow implements **fork-safe secret handling patterns**:

### Expected Behavior (Not Errors)
- **Secret context warnings** in GitHub Actions workflows are **expected and correct**
- Secrets are intentionally unavailable in fork PRs for security
- Workflows gracefully handle missing secrets using conditional logic

### Trunk Configuration for Secrets
```yaml
# Actionlint configured to allow GitHub Actions context warnings
- linters: [actionlint]
  where: |
    # Allow context warnings - this is expected per GitHub docs
    message.contains("Context access might be invalid")
```

### Reference Documentation
- **GitHub Secrets Context**: https://docs.github.com/en/actions/reference/workflows-and-actions/contexts#secrets-context
- **Fork PR Security**: Secrets not available in external contributions (correct behavior)
- **Conditional Secret Usage**: Use `if:` conditions and graceful degradation

## 🛠️ Key Settings

Configuration in `.vscode/settings.json`:
- `trunk.enabled: true`
- `trunk.autoRun: true` (runs on save for targeted languages)
- `trunk.languages: [csharp, powershell, xml, sql, yaml]`
- `trunk.linters`:
    - **PSScriptAnalyzer**: PowerShell linting with Microsoft standards
    - **Roslynator**: C# analyzers & refactorings
    - **XamlStyler**: XAML formatting with Syncfusion compliance
    - **Actionlint**: GitHub Actions with secret-aware configuration

## 🎯 How to Use Locally

1. **Install Extensions**: VS Code will prompt from `.vscode/extensions.json`
2. **Auto-Run**: Trunk runs automatically on save
3. **Manual Check**: Use Command Palette → "Trunk: Check"
4. **Manual Fix**: Use Command Palette → "Trunk: Fix"

## 📋 Repository Policies

- **Canonical Formatter**: Trunk is the single source of truth for formatting/linting
- **No Conflicts**: Don't add alternative formatters in `.vscode/extensions.json`
- **Syncfusion Compliance**: Follow official Syncfusion docs + ensure Trunk passes
- **Secret Warnings**: Expected in GitHub Actions - not actual errors

## 🔄 CI/Pre-commit Integration

### PowerShell Commands
```powershell
# Quality check (includes Trunk)
bb-quality-check

# Anti-regression (includes secret pattern validation)
bb-anti-regression

# Direct Trunk usage
trunk check --all
trunk fmt --all
```

### Pre-commit Hooks
- `trunk-fmt-pre-commit`: Auto-formats on commit (includes whitespace trimming)
- `trunk-check-pre-push`: Quality check before push

## 🚨 Troubleshooting

### "Context access might be invalid" in GitHub Actions
- ✅ **This is expected and correct** per GitHub documentation
- ✅ Secrets are intentionally unavailable in fork PRs
- ✅ Workflow handles this gracefully with conditional logic

### PowerShell Linting Issues
- Settings in `PSScriptAnalyzerSettings.psd1` at repository root
- Follows Microsoft PowerShell standards compliance

### XAML/Syncfusion Issues
- Ensure Syncfusion namespace declarations are correct
- Follow official Syncfusion WPF documentation patterns
- Use `Settings.XamlStyler` for consistent formatting

## 📚 Reference Links

- **Trunk Documentation**: https://docs.trunk.io/reference/trunk-yaml
- **GitHub Secrets Context**: https://docs.github.com/en/actions/reference/workflows-and-actions/contexts#secrets-context
- **Microsoft PowerShell Standards**: https://docs.microsoft.com/en-us/powershell/scripting/developer/cmdlet/cmdlet-development-guidelines
- **Syncfusion WPF Documentation**: https://help.syncfusion.com/wpf/welcome-to-syncfusion-essential-wpf
