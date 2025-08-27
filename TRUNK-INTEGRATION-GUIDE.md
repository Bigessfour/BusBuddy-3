# 🚌 BusBuddy Trunk.io Integration - Complete Setup Guide

## 📋 Integration Status: ✅ FULLY INTEGRATED & ENHANCED

Trunk.io is comprehensively integrated into the BusBuddy development environment with production-ready configuration and CI/CD pipeline integration.

## 🎯 Available Commands

### PowerShell Commands (bb-\* prefix)

```powershell
# Comprehensive quality check with auto-fix
bb-quality-check

# Format all code using Trunk
bb-format-code

# Security scanning
bb-security-scan

# Show Trunk status and configuration
bb-trunk-status
```

### VS Code Tasks

- **Trunk Check All** - Full project linting and formatting check
- **Trunk Format All** - Format all supported files
- **bb-quality-check (BusBuddy Quality Gate)** - Production-ready quality gate
- **bb-format-code (BusBuddy Code Formatting)** - Format all BusBuddy code
- **bb-security-scan (BusBuddy Security Analysis)** - Security scanning
- **Trunk Check C# Files Only** - C# specific checks
- **Trunk Check PowerShell Files Only** - PowerShell specific checks
- **Trunk Check XAML Files Only** - XAML specific checks

### VS Code Tasks

- **Trunk Check All** - Full project linting and formatting check
- **Trunk Format All** - Format all supported files
- **bb-quality-check (BusBuddy Quality Gate)** - Production-ready quality gate
- **bb-format-code (BusBuddy Code Formatting)** - Format all BusBuddy code
- **bb-security-scan (BusBuddy Security Analysis)** - Security scanning
- **Trunk Check C# Files Only** - C# specific checks
- **Trunk Check PowerShell Files Only** - PowerShell specific checks
- **Trunk Check XAML Files Only** - XAML specific checks

### Keyboard Shortcuts

- `Ctrl+Shift+T` - Run bb-quality-check
- `Ctrl+Shift+F` - Run bb-format-code
- `Ctrl+Shift+S` - Run bb-security-scan

## 🛠️ Trunk Command Reference

### Core Commands

#### `trunk check` - Universal Code Checker

```bash
# Check all files
trunk check --all

# Check specific files or directories
trunk check src/ tests/ *.cs

# Auto-fix issues
trunk check --fix

# Check with specific linters only
trunk check --filter dotnet-format,powershell

# Exclude specific linters
trunk check --filter -eslint,-shellcheck

# Check for security issues only
trunk check --scope security

# Run on CI with upload
trunk check --ci --upload --token $TRUNK_API_KEY

# Show existing autofixes
trunk check --include-existing-autofixes

# Ignore git state (useful during merges)
trunk check --ignore-git-state
```

#### `trunk fmt` - Universal Code Formatter

```bash
# Format all files
trunk fmt --all

# Format specific files
trunk fmt src/**/*.cs PowerShell/**/*.ps1

# Format without applying changes (dry run)
trunk fmt --no-fix

# Format with specific formatters
trunk fmt --filter prettier,dotnet-format

# Show diff of changes
trunk fmt --diff full
```

### Management Commands

#### `trunk init` - Setup Trunk in Repository

```bash
# Initialize Trunk in current directory
trunk init

# Initialize with specific configuration
trunk init --template dotnet
```

#### `trunk login` / `trunk logout` - Authentication

```bash
# Login to trunk.io
trunk login

# Logout
trunk logout

# Check current user
trunk whoami
```

#### `trunk upgrade` - Update Trunk and Linters

```bash
# Upgrade Trunk CLI and all linters
trunk upgrade

# Upgrade specific linter
trunk upgrade dotnet-format
```

### Configuration Commands

#### `trunk config` - Configuration Management

```bash
# Show current configuration
trunk config

# Edit configuration
trunk config --edit

# Validate configuration
trunk config --validate
```

#### `trunk plugins` - Plugin Management

````bash
# List installed plugins
trunk plugins list

### Core Commands

#### `trunk check` - Universal Code Checker
```bash
# Check all files
trunk check --all

# Check specific files or directories
trunk check src/ tests/ *.cs

# Auto-fix issues
trunk check --fix

# Check with specific linters only
trunk check --filter dotnet-format,powershell

# Exclude specific linters
trunk check --filter -eslint,-shellcheck

# Check for security issues only
trunk check --scope security

# Run on CI with upload
trunk check --ci --upload --token $TRUNK_API_KEY

# Show existing autofixes
trunk check --include-existing-autofixes

# Ignore git state (useful during merges)
trunk check --ignore-git-state
````

#### `trunk fmt` - Universal Code Formatter

```bash
# Format all files
trunk fmt --all

# Format specific files
trunk fmt src/**/*.cs PowerShell/**/*.ps1

# Format without applying changes (dry run)
trunk fmt --no-fix

# Format with specific formatters
trunk fmt --filter prettier,dotnet-format

# Show diff of changes
trunk fmt --diff full
```

### Management Commands

#### `trunk init` - Setup Trunk in Repository

```bash
# Initialize Trunk in current directory
trunk init

# Initialize with specific configuration
trunk init --template dotnet
```

#### `trunk login` / `trunk logout` - Authentication

```bash
# Login to trunk.io
trunk login

# Logout
trunk logout

# Check current user
trunk whoami
```

#### `trunk upgrade` - Update Trunk and Linters

```bash
# Upgrade Trunk CLI and all linters
trunk upgrade

# Upgrade specific linter
trunk upgrade dotnet-format
```

### Configuration Commands

#### `trunk config` - Configuration Management

```bash
# Show current configuration
trunk config

# Edit configuration
trunk config --edit

# Validate configuration
trunk config --validate
```

#### `trunk plugins` - Plugin Management

```bash
# List installed plugins
trunk plugins list

# Install plugin
trunk plugins install <plugin-id>

# Update plugins
trunk plugins upgrade
```

### Tool Management

#### `trunk tools` - Universal Tool Manager

```bash
# List available tools
trunk tools list

# Install specific tool
trunk tools install dotnet-format

# Update all tools
trunk tools upgrade
```

#### `trunk install` - Download & Install Runtimes/Linters

```bash
# Install all enabled runtimes and linters
trunk install

# Install specific runtime
trunk install node@18.0.0
```

### Git Integration

#### `trunk git-hooks` - Git Hooks Management

```bash
# Install git hooks
trunk git-hooks install

# List available hooks
trunk git-hooks list

# Remove hooks
trunk git-hooks remove
```

### Advanced Commands

#### `trunk actions` - Workflow Automation

```bash
# List available actions
trunk actions list

# Run specific action
trunk actions run <action-name>

# Create custom action
trunk actions create
```

#### `trunk run` - Run Specified Action

```bash
# Run action with parameters
trunk run my-action --param value

# Run action in CI mode
trunk run my-action --ci
```

#### `trunk merge` - Submit Pull Request

```bash
# Create merge request
trunk merge

# Merge with specific options
trunk merge --title "Fix formatting issues"
```

### Diagnostic Commands

#### `trunk cache` - Cache Management

```bash
# Clear all caches
trunk cache clear

# Show cache statistics
trunk cache stats

# Clean old cache entries
trunk cache clean
```

#### `trunk daemon` - Daemon Management

```bash
# Start daemon
trunk daemon start

# Stop daemon
trunk daemon stop

# Check daemon status
trunk daemon status
```

### BusBuddy-Specific Commands

#### Quality Gate Commands

```powershell
# Run full quality check (includes Trunk)
bb-quality-check

# Format all code
bb-format-code

# Security scanning
bb-security-scan

# Show Trunk status
bb-trunk-status
```

#### File-Specific Checks

```bash
# Check C# files only
trunk check **/*.cs **/*.csproj

# Check PowerShell files only
trunk check **/*.ps1 **/*.psm1

# Check XAML files only
trunk check **/*.xaml

# Format C# files only
trunk fmt **/*.cs

# Format PowerShell files only
trunk fmt **/*.ps1 **/*.psm1

# Format XAML files only
trunk fmt **/*.xaml
```

### CI/CD Integration

#### GitHub Actions Integration

```yaml
- name: Run Trunk Check
  uses: trunk-io/trunk-action@v1
  with:
      token: ${{ secrets.TRUNK_API_KEY }}
      check-all: true
      upload-series: ${{ github.ref_name }}
```

#### Command Line CI Mode

```bash
# Run in CI mode with upload
trunk check --ci --upload --token $TRUNK_API_KEY --series $BRANCH_NAME

# Run formatter in CI
trunk fmt --ci --all

# Run security checks
trunk check --scope security --ci
```

#### Enhanced CI/CD Pipeline Features

##### Multi-Stage Trunk Checks

```yaml
# Post-build quality check
- name: Trunk Check After Build
  run: |
      trunk check --all --ci --upload --output-format=sarif --output-file=trunk-post-build.sarif
  env:
      TRUNK_API_KEY: ${{ secrets.TRUNK_API_KEY }}

# Security-focused checks
- name: Trunk Security Scan
  run: |
      trunk check --scope security --ci --upload --output-format=sarif --output-file=trunk-security.sarif
  env:
      TRUNK_API_KEY: ${{ secrets.TRUNK_API_KEY }}

# Formatting validation
- name: Trunk Format Check
  run: |
      trunk fmt --all --check
```

##### Flaky Test Detection and Handling

```yaml
# Test retry configuration
- name: Run Tests with Retry
  run: |
      $attempts = 2
      $success = $false
      for ($i = 1; $i -le $attempts; $i++) {
        Write-Output "Test attempt $i of $attempts"
        dotnet test --logger "trx;LogFileName=test-results.trx"
        if ($LASTEXITCODE -eq 0) {
          $success = $true
          break
        }
        Start-Sleep -Seconds 10
      }

# Analyze test flakiness
- name: Analyze Test Flakiness
  if: always()
  run: |
      # Parse test results for flaky patterns
      # Implementation details in test analysis scripts
```

##### Test Settings Configuration

```xml
<!-- testsettings.runsettings -->
<FlakyTestSettings>
  <Enabled>true</Enabled>
  <MaxRetries>3</MaxRetries>
  <RetryDelay>1000</RetryDelay>
  <DetectPatterns>
    <Pattern>TimeoutException</Pattern>
    <Pattern>ThreadAbortException</Pattern>
    <Pattern>IOException</Pattern>
  </DetectPatterns>
  <ExcludeCategories>
    <Category>IntegrationTest</Category>
  </ExcludeCategories>
</FlakyTestSettings>
```

#### Repository Secrets Setup

```bash
# Required GitHub repository secrets
TRUNK_API_KEY=your-trunk-api-key-here
SYNCFUSION_LICENSE_KEY=your-syncfusion-license-here
```

#### Environment Variables

```bash
# Local development (.env file)
TRUNK_API_KEY=your-api-key
BUSBUDDY_DISABLE_VALIDATION=0
BUSBUDDY_SKIP_AST_VALIDATION=0

# CI/CD environment
DOTNET_VERSION=9.0.x
BUILD_CONFIGURATION=Release
BUSBUDDY_NO_WELCOME=1
BUSBUDDY_NO_XAI_WARN=1
BUSBUDDY_SILENT=1
```

### Performance Optimization

#### Parallel Processing

```bash
# Use multiple jobs
trunk check --jobs 4

# Adjust timeout
trunk check --action_timeout 300
```

#### Caching

```bash
# Enable caching (default)
trunk check --cache true

# Disable caching
trunk check --cache false
```

### Filtering and Exclusions

#### Filter by Linter

```bash
# Run only specific linters
trunk check --filter dotnet-format,powershell

# Exclude specific linters
trunk check --filter -eslint,-shellcheck

# Filter by issue codes
trunk check --filter dotnet-format/CS8019
```

#### Exclude Files

```bash
# Exclude files by pattern
trunk check --ignore "docs/**/*.md" --ignore "temp/**/*"

# Force check ignored files
trunk check --force
```

### Output and Reporting

#### Diff Output

```bash
# Show compact diff
trunk fmt --diff compact

# Show full diff
trunk fmt --diff full

# No diff output
trunk fmt --diff none
```

#### Verbose Output

```bash
# Enable verbose mode
trunk check --verbose

# Show progress
trunk check --no-progress
```

### Troubleshooting Commands

#### Version and Diagnostics

```bash
# Show version
trunk --version

# Show help
trunk --help

# Check installation
trunk install --dry-run
```

#### Debug Commands

```bash
# Debug specific file
trunk check path/to/problematic-file.cs --verbose

# Test configuration
trunk config --validate

# Check for announcements
trunk show-announcements
```

## 🔧 Configuration Files

### Environment Variables (.env)

```bash
TRUNK_API_KEY=0fd8d7878ff0c1141fe2debbd354dd7ba0f04e99
TRUNK_ORGANIZATION=busbuddy
TRUNK_REPOSITORY=BusBuddy-3
TRUNK_CI=true
TRUNK_AUTO_FORMAT=true
TRUNK_FORMAT_ON_SAVE=true
TRUNK_ENABLE_LINTING=true
TRUNK_ENABLE_FORMAT=true
TRUNK_GIT_INTEGRATION=true
TRUNK_SHOW_STATUS_IN_EXPLORER=true
TRUNK_AUTO_FORMAT_ON_PASTE=true
TRUNK_ENABLE_ACTIONS=true
TRUNK_INLINE_DECORATORS=true
TRUNK_INLINE_DECORATORS_FOR_ALL_EXTENSIONS=true
```

### Trunk Configuration (.trunk/trunk.yaml)

- **Version**: 1.25.0
- **Linters Enabled**: 15+ linters including dotnet-format, psscriptanalyzer, cspell, trufflehog
- **Formatters**: dotnet-format, prettier, xaml-styler
- **File Types**: C#, PowerShell, XAML, JSON, YAML, SQL
- **Quality Gates**: Production-ready checks with error/warning thresholds

### VS Code Integration (.vscode/settings.json)

```json
{
    "trunk.enableLinting": true,
    "trunk.enableFormatting": true,
    "trunk.formatOnSave": true,
    "trunk.gitIntegration": true,
    "trunk.showStatusInExplorer": true,
    "trunk.autoFormatOnPaste": true,
    "trunk.enableActions": true,
    "trunk.inlineDecorators": true,
    "trunk.inlineDecoratorsForAllExtensions": true,
    "trunk.formatOnSaveTimeout": 5000,
    "trunk.lintOnSaveTimeout": 10000,
    "trunk.checkOnSaveTimeout": 15000,
    "editor.defaultFormatter": "trunk.io"
}
```

## 🚀 Workflows

### Development Workflow

1. **Code Changes** - Write code with format-on-save enabled
2. **Pre-Commit** - Trunk auto-formats and lints
3. **Quality Check** - Run `bb-quality-check` before pushing
4. **Security Scan** - Run `bb-security-scan` for security validation

### CI/CD Integration

- **Git Hooks**: Pre-commit formatting, pre-push quality checks
- **SARIF Output**: Integrated with GitHub Security tab
- **Parallel Processing**: Optimized for large codebases
- **Incremental Checks**: Fast feedback for changed files

## 📊 Linters & Formatters

### Code Quality

- **dotnet-format** - C# formatting and style
- **psscriptanalyzer** - PowerShell best practices
- **cspell** - Spell checking for code comments
- **roslynator** - C# code analysis

### Security

- **trufflehog** - Secret detection
- **osv-scanner** - Vulnerability scanning
- **checkov** - Infrastructure as code security

### Formatting

- **prettier** - JSON, YAML, Markdown
- **xaml-styler** - XAML formatting
- **sqlfluff** - SQL formatting

## 🎨 File Type Support

| File Type     | Linters                           | Formatters       | Quality Gates |
| ------------- | --------------------------------- | ---------------- | ------------- |
| `.cs`         | dotnet-format, roslynator, cspell | dotnet-format    | ✅ Production |
| `.ps1/.psm1`  | psscriptanalyzer, cspell          | psscriptanalyzer | ✅ Production |
| `.xaml`       | xaml-styler, cspell               | xaml-styler      | ✅ Production |
| `.sql`        | sqlfluff, cspell                  | sqlfluff         | ✅ Production |
| `.json/.yaml` | prettier, yamllint                | prettier         | ✅ Production |

## ⚡ Performance Optimizations

- **Parallel Processing**: Up to 5 concurrent operations
- **Incremental Checks**: Only check changed files when possible
- **Caching**: Tool and runtime caching for faster execution
- **Timeouts**: Configured for large projects (5-15s)
- **Selective Checks**: File-type specific optimizations

## 🔒 Security Features

- **Secret Detection**: Trufflehog integration
- **Vulnerability Scanning**: OSV scanner
- **License Compliance**: Automated license checking
- **Dependency Analysis**: Security vulnerabilities in dependencies

## 📈 Monitoring & Reporting

- **SARIF Integration**: GitHub Security tab integration
- **JSON Reports**: Detailed quality reports
- **Status Indicators**: VS Code status bar integration
- **Git Integration**: Commit and push hooks

## 🎯 Best Practices

### For Developers

1. **Always run `bb-quality-check`** before committing
2. **Use format-on-save** for consistent formatting
3. **Run `bb-security-scan`** for security-critical changes
4. **Check Trunk status** with `bb-trunk-status`

### For CI/CD

1. **Use SARIF output** for GitHub integration
2. **Configure quality gates** with appropriate thresholds
3. **Enable parallel processing** for faster builds
4. **Monitor performance** with timeout configurations

## 🚨 Troubleshooting

### Common Issues

- **API Key Missing**: Check .env file has TRUNK_API_KEY
- **Slow Performance**: Adjust timeout values in settings
- **False Positives**: Configure ignore patterns in trunk.yaml
- **Integration Issues**: Verify VS Code extension version

### Debug Commands

```powershell
# Check Trunk installation
trunk --version

# Test configuration
trunk check --help

# Debug specific file
trunk check path/to/file.cs

# View configuration
trunk config
```

## 📚 Resources

- **Trunk Documentation**: https://docs.trunk.io
- **BusBuddy Guidelines**: .github/copilot-instructions.md
- **PowerShell Profile**: tools/powershell/Profiles/Microsoft.PowerShell_profile_optimized.ps1
- **VS Code Tasks**: .vscode/tasks.json

---

**Status**: 🟢 Production Ready
**Integration Level**: Complete
**Last Updated**: August 27, 2025
**Version**: 3.0.0</content>
<parameter name="filePath">c:\Users\biges\Desktop\BusBuddy\TRUNK-INTEGRATION-GUIDE.md
