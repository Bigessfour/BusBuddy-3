# Example: How to Call build-reusable.yml from Other Workflows

This documents how calling workflows should pass secrets to `build-reusable.yml`.

## ✅ **RECOMMENDED: secrets: inherit Pattern (VS Code Compatible)**

```yaml
jobs:
  call-reusable-build:
    name: 🏗️ Build using Reusable Workflow
    uses: ./.github/workflows/build-reusable.yml
    with:
      configuration: 'Release'
      run-tests: true
      upload-artifacts: true
    secrets: inherit  # Automatically passes ALL repository secrets
```

## 🔧 **Alternative: Explicit Secrets Pattern**

If `secrets: inherit` doesn't work in your environment:

```yaml
jobs:
  call-reusable-build:
    name: 🏗️ Build using Reusable Workflow
    uses: ./.github/workflows/build-reusable.yml
    with:
      configuration: 'Release'
      run-tests: true
      upload-artifacts: true
    secrets:
      SYNC_LICENSE_KEY: ${{ secrets.SYNC_LICENSE_KEY }}
      CODECOV_TOKEN: ${{ secrets.CODECOV_TOKEN }}
      GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
```

## 🔧 **Secret Inheritance Pattern**

When calling the reusable workflow, you must explicitly pass secrets:

```yaml
secrets:
  # Required for Syncfusion license registration
  SYNC_LICENSE_KEY: ${{ secrets.SYNC_LICENSE_KEY }}
  
  # Required for code coverage uploads (if using Codecov)
  CODECOV_TOKEN: ${{ secrets.CODECOV_TOKEN }}
  
  # Required for artifact uploads (usually inherited automatically)
  GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
```

## ⚠️ **VS Code Extension Compatibility Fix**

**Problem:** The error "Unrecognized named-value: 'secrets'" occurs when the GitHub Actions extension can't parse complex secret definitions.

**Solution Options (in order of preference):**

1. **Use `secrets: inherit`** - Automatically passes all repository secrets
2. **Update VS Code Extension** - Reload extension (Ctrl+Shift+P → Extensions: Reload)
3. **Use explicit secrets** - Define each secret individually

**Verification Steps:**
1. Save YAML files
2. Reload VS Code window (Ctrl+Shift+P → Developer: Reload Window)
3. Push to GitHub and check Actions tab for parse errors

## 🔥 **secrets: inherit Benefits**

- ✅ **VS Code Compatible** - No extension parsing issues
- ✅ **Simpler Syntax** - No need to list every secret
- ✅ **Automatic Updates** - New secrets automatically available
- ✅ **Less Maintenance** - No need to update caller workflows when secrets change

## 📋 **Available Secrets**

| Secret | Description | Required | Fallback |
|--------|-------------|----------|----------|
| `SYNC_LICENSE_KEY` | Syncfusion License Key | No | Community license |
| `CODECOV_TOKEN` | Codecov upload token | No | Skip coverage upload |
| `GITHUB_TOKEN` | GitHub API access token | No | `github.token` context |

## �️ **VS Code Extension Update Instructions**

**Steps to fix GitHub Actions extension parsing issues:**

1. **Update Extension:**
   - Press `Ctrl+Shift+P`
   - Type "Extensions: Show Installed Extensions"
   - Find "GitHub Actions" extension
   - Click "Update" if available (ensure version 0.28+ as of 2025)

2. **Reload Extension:**
   - Press `Ctrl+Shift+P`
   - Type "Extensions: Reload"
   - Select the GitHub Actions extension

3. **Reload VS Code Window:**
   - Press `Ctrl+Shift+P`
   - Type "Developer: Reload Window"
   - Select to reload entire VS Code window

4. **Clear Extension Cache:**
   - Close VS Code completely
   - Navigate to `%USERPROFILE%\.vscode\extensions`
   - Delete `github.vscode-github-actions-*` folder
   - Restart VS Code and reinstall extension

**Verification:**
- Open any `.github/workflows/*.yml` file
- Check if syntax highlighting works correctly
- Verify no "Unrecognized named-value: 'secrets'" errors appear
- Test workflow validation and IntelliSense
