# 🚌 BusBuddy Trunk.io Configuration

This directory contains Trunk.io configuration files for centralized linting and formatting across the BusBuddy project.

## 📁 Configuration Files

### `.trunk/trunk.yaml`
Main Trunk configuration file that:
- Configures linters for PowerShell, YAML, JSON, and security scanning
- Sets up file exclusions for build artifacts and logs
- Integrates with PSScriptAnalyzer using project-specific settings
- Provides VS Code integration via SARIF output format

### `.trunk/configs/`
- **`PSScriptAnalyzerSettings.psd1`** - PowerShell linting rules (copy from project root)
- **`.sqlfluff`** - Azure SQL/T-SQL formatting configuration
- **`roslynator.config.json`** - C# analysis rules for WPF .NET 9
- **`.yamllint`** - YAML formatting rules for Azure DevOps and GitHub Actions

## 🚀 Usage Commands

### Basic Linting
```bash
# Check all changed files
trunk check

# Check all files
trunk check --all

# Check specific files/directories
trunk check BusBuddy.WPF/ PowerShell/

# Check with auto-fix
trunk check --fix
```

### Formatting
```bash
# Format all files
trunk fmt --all

# Format specific files
trunk fmt PowerShell/*.ps1
```

### Sample Testing
```bash
# Test on a few files
trunk check --sample=5

# Test specific linters
trunk check --filter=psscriptanalyzer,yamllint
```

## 🔧 Enabled Linters

| Linter | Purpose | Files |
|--------|---------|-------|
| **psscriptanalyzer** | PowerShell best practices | `*.ps1`, `*.psm1`, `*.psd1` |
| **yamllint** | YAML formatting | `*.yaml`, `*.yml` |
| **prettier** | JSON formatting | `*.json` |
| **checkov** | Infrastructure security | CI/CD files |
| **trufflehog** | Secret detection | All files |
| **osv-scanner** | Vulnerability scanning | Dependencies |
| **git-diff-check** | Git hygiene | Git tracked files |

## 🚫 Excluded Paths

- `**/bin/**` - Build outputs
- `**/obj/**` - Build intermediates  
- `**/TestResults/**` - Test outputs
- `**/logs/**` - Log files
- `**/artifacts/**` - Build artifacts
- `**/*.disabled` - Disabled files for clean builds
- `**/Migrations/**` - EF auto-generated code

## 🔗 Integration

### VS Code
Trunk integrates with VS Code via:
- SARIF output format for Problems panel
- `trunk check` available in Command Palette
- Task Explorer integration (use "Trunk Check All" task)

### PowerShell Profile
The BusBuddy PowerShell profile includes trunk shortcuts:
```powershell
trunk check --all    # Full project check
trunk fmt --all      # Format all files
```

## 📋 VS Code Tasks

Use these tasks from Task Explorer:
- **"Trunk Check All"** - Full project linting
- **"Trunk Format All"** - Format all project files
- **"PSScriptAnalyzer (PowerShell Quality)"** - PowerShell-specific analysis

## 🛠️ Customization

### Adding New Linters
1. Edit `.trunk/trunk.yaml`
2. Add linter to `lint.enabled` section
3. Configure any file patterns in `include`/`exclude`
4. Add configuration file to `.trunk/configs/` if needed

### PowerShell Rules
Modify `PSScriptAnalyzerSettings.psd1` to adjust PowerShell linting rules. The file is automatically used by Trunk's psscriptanalyzer integration.

### SQL Rules  
Edit `.trunk/configs/.sqlfluff` to customize Azure SQL formatting rules.

## 🚀 Getting Started

1. **Install Trunk** (if not already installed):
   ```bash
   curl https://get.trunk.io -fsSL | bash
   ```

2. **Run initial check**:
   ```bash
   trunk check --sample=10
   ```

3. **Fix auto-fixable issues**:
   ```bash
   trunk check --fix --all
   ```

4. **Commit configuration**:
   ```bash
   git add .trunk/
   git commit -m "feat: configure Trunk.io for centralized linting"
   ```

## 📖 References

- [Trunk.io Documentation](https://docs.trunk.io/)
- [PSScriptAnalyzer Rules](https://docs.microsoft.com/en-us/powershell/utility-modules/psscriptanalyzer/)
- [SQLFluff Documentation](https://docs.sqlfluff.com/)
- [YAML Lint Documentation](https://yamllint.readthedocs.io/)
