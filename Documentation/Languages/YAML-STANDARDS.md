# 🌊 YAML Standards for BusBuddy

## **Official YAML Standards**
- **Core Specification**: [YAML 1.2.2 Specification](https://yaml.org/spec/1.2.2/)
- **Official Website**: [YAML.org](https://yaml.org/)
- **GitHub Actions Schema**: [GitHub Actions Workflow Syntax](https://docs.github.com/en/actions/using-workflows/workflow-syntax-for-github-actions)

## **YAML Usage in BusBuddy**

#### **1. CodeCov Configuration (Deprecated)**
Codecov usage has been deprecated in this project. External uploads have been removed from CI.

Recommended approach now:
- Generate coverage with `dotnet test --collect:"XPlat Code Coverage"`.
- Consume Cobertura reports from `TestResults/**/coverage.cobertura.xml`.
- Rely on CI summary and uploaded artifacts instead of third‑party dashboards.

Example CI snippet (already implemented in `.github/workflows/ci.yml`):
```yaml
- name: 📊 Generate coverage report
  run: |
    $coverageFiles = Get-ChildItem -Path "./TestResults" -Filter "coverage.cobertura.xml" -Recurse
    if ($coverageFiles.Count -gt 0) {
      [xml]$coverage = Get-Content $coverageFiles[0].FullName
      $lineRate = [double]$coverage.coverage.'line-rate'
      $coveragePercent = [math]::Round($lineRate * 100, 2)
      echo "✅ Coverage: $coveragePercent%"
    } else {
      echo "⚠️ No coverage files found"
    }
  - item3

# Objects/Mappings
object_example:
  property1: value1
  property2: value2
  nested_object:
    nested_property: nested_value

# Multi-line strings
multiline_literal: |
  This is a literal string
  that preserves line breaks
  and formatting

multiline_folded: >
  This is a folded string
  that converts line breaks
  to spaces

...  # Document end marker (optional)
```

#### **2. Indentation Standards**
- **Use 2 spaces** for indentation (consistent with GitHub Actions)
- **No tabs** - only spaces
- **Consistent nesting** - maintain alignment

```yaml
name: Build and Test

on:
  push:
    branches: [main, master]
  pull_request:
    branches: [main, master]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout code
        uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
```

#### **3. Naming Conventions**
- **Keys**: Use `snake_case` or `kebab-case` consistently
- **Environment Variables**: Use `UPPER_SNAKE_CASE`
- **GitHub Actions**: Follow GitHub naming conventions

### ✅ **GitHub Actions-Specific Standards**

#### **1. Workflow Structure**
```yaml
name: 🚌 Bus Buddy CI/CD Pipeline

# Trigger configuration
on:
  push:
    branches: [main, master]
    paths:
      - 'src/**'
      - 'tests/**'
      - '.github/workflows/**'
  pull_request:
    branches: [main, master]
  workflow_dispatch:
    inputs:
      debug_enabled:
        type: boolean
        description: 'Enable debug mode'
        default: false

# Environment variables
env:
  DOTNET_VERSION: '8.0.x'
  BUILD_CONFIGURATION: 'Release'
  SOLUTION_FILE: 'BusBuddy.sln'

# Job definitions
jobs:
  build:
    name: Build and Test
    runs-on: windows-latest

    steps:
      - name: 📥 Checkout repository
        uses: actions/checkout@v4
        with:
          fetch-depth: 0  # Full history for proper versioning

      - name: 🏗️ Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: 📦 Restore dependencies
        run: dotnet restore ${{ env.SOLUTION_FILE }}

      - name: 🔨 Build solution
        run: |
          dotnet build ${{ env.SOLUTION_FILE }} \
            --configuration ${{ env.BUILD_CONFIGURATION }} \
            --no-restore \
            --verbosity minimal
```

#### **2. Job Organization**
```yaml
jobs:
  # Build job
  build:
    name: 🔨 Build
    runs-on: windows-latest
    outputs:
      build-version: ${{ steps.version.outputs.version }}
    steps:
      # Build steps here

  # Test job (depends on build)
  test:
    name: 🧪 Test
    runs-on: windows-latest
    needs: build
    steps:
      # Test steps here

  # Deploy job (depends on test)
  deploy:
    name: 🚀 Deploy
    runs-on: windows-latest
    needs: [build, test]
    if: github.ref == 'refs/heads/main'
    steps:
      # Deploy steps here
```

#### **3. Secrets and Variables**
```yaml
# Using repository secrets
env:
  SYNCFUSION_LICENSE_KEY: ${{ secrets.SYNCFUSION_LICENSE_KEY }}
  DATABASE_CONNECTION: ${{ secrets.DATABASE_CONNECTION }}

# Using repository variables
env:
  BUILD_CONFIGURATION: ${{ vars.BUILD_CONFIGURATION }}

# Input parameters
inputs:
  environment:
    description: 'Target environment'
    required: true
    type: choice
    options:
      - development
      - staging
      - production
    default: 'development'
```

### ✅ **Configuration File Standards**

#### **1. CodeCov Configuration**
```yaml
# Codecov configuration for Bus Buddy WPF project
# Documentation: https://docs.codecov.com/docs/codecov-yaml

codecov:
  # General settings
  require_ci_to_pass: true
  disable_default_path_fixes: false

  # Notification settings
  notify:
    after_n_builds: 1
    wait_for_ci: true

# Coverage configuration
coverage:
  precision: 2
  round: down
  range: "70...95"

  status:
    project:
      default:
        target: 80%
        threshold: 5%
    patch:
      default:
        target: 70%

  ignore:
    - "tests/**/*"
    - "**/*.Designer.cs"
    - "**/obj/**/*"
    - "**/bin/**/*"

# Comment configuration
comment:
  layout: "reach,diff,flags,tree,reach"
  behavior: default
  require_changes: false
```

### ✅ **Security Standards**

#### **1. Secrets Management**
```yaml
# ❌ DON'T: Hardcode secrets
env:
  API_KEY: "hardcoded-secret-key"  # NEVER DO THIS

# ✅ DO: Use GitHub secrets
env:
  API_KEY: ${{ secrets.API_KEY }}

# ✅ DO: Use conditional secrets
env:
  API_KEY: ${{ github.ref == 'refs/heads/main' && secrets.PROD_API_KEY || secrets.DEV_API_KEY }}
```

#### **2. Permission Configuration**
```yaml
# Set minimal required permissions
permissions:
  contents: read
  checks: write
  pull-requests: write

# Or use specific permissions for sensitive operations
permissions:
  contents: write  # For creating releases
  packages: write  # For publishing packages
  security-events: write  # For security scanning
```

### ✅ **Performance Standards**

#### **1. Caching Strategy**
```yaml
- name: 📦 Cache NuGet packages
  uses: actions/cache@v4
  with:
    path: |
      ~/.nuget/packages
      !~/.nuget/packages/*/
    key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj', '**/packages.lock.json') }}
    restore-keys: |
      ${{ runner.os }}-nuget-

- name: 🏗️ Cache build output
  uses: actions/cache@v4
  with:
    path: |
      **/bin
      **/obj
    key: ${{ runner.os }}-build-${{ hashFiles('**/*.csproj') }}-${{ github.sha }}
    restore-keys: |
      ${{ runner.os }}-build-${{ hashFiles('**/*.csproj') }}-
```

#### **2. Matrix Builds**
```yaml
strategy:
  matrix:
    os: [windows-latest, ubuntu-latest]
    dotnet-version: ['8.0.x']
    configuration: ['Debug', 'Release']
  fail-fast: false  # Continue other jobs if one fails
  max-parallel: 4   # Limit concurrent jobs

runs-on: ${{ matrix.os }}
```

## **BusBuddy-Specific YAML Patterns**

### **Complete Workflow Template**
```yaml
name: 🚌 Bus Buddy Comprehensive Pipeline

on:
  push:
    branches: [main, master]
  pull_request:
    branches: [main, master]
  workflow_dispatch:

env:
  DOTNET_VERSION: '8.0.x'
  SOLUTION_FILE: 'BusBuddy.sln'

jobs:
  validate:
    name: 🔍 Validate
    runs-on: windows-latest
    steps:
      - name: 📥 Checkout
        uses: actions/checkout@v4

      - name: 🔧 Validate YAML
        run: |
          # Add YAML validation commands

  build:
    name: 🔨 Build
    needs: validate
    runs-on: windows-latest
    steps:
      - name: 📥 Checkout
        uses: actions/checkout@v4

      - name: 🏗️ Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: 📦 Restore
        run: dotnet restore ${{ env.SOLUTION_FILE }}

      - name: 🔨 Build
        run: dotnet build ${{ env.SOLUTION_FILE }} --no-restore
```

## **Validation Commands**

```powershell
# Validate YAML syntax using PowerShell (built-in methods preferred)
try {
    $yamlContent = Get-Content "file.yml" -Raw
    # Use built-in validation methods instead of external modules
    Write-Host "✅ YAML file loaded successfully" -ForegroundColor Green
} catch {
    Write-Host "❌ Invalid YAML: $($_.Exception.Message)" -ForegroundColor Red
}

# Use native dotnet tools for validation
# dotnet tool install --global yamllint-cli (if available)
# Or use online YAML validators for validation
```

## **Common Pitfalls and Solutions**

### ❌ **Common Mistakes**
```yaml
# DON'T: Inconsistent indentation
name: Bad Example
jobs:
build:  # Missing indentation
  runs-on: ubuntu-latest
    steps:  # Wrong indentation
  - name: Step  # Inconsistent list format

# DON'T: Unquoted version numbers that look like floats
dotnet-version: 8.0  # Interpreted as float, becomes 8

# DON'T: Hardcoded secrets
env:
  SECRET_KEY: "actual-secret-value"
```

### ✅ **Correct Patterns**
```yaml
# DO: Consistent indentation
name: Good Example
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - name: Step 1
        run: echo "Hello"

# DO: Quote version numbers
dotnet-version: '8.0'

# DO: Use secrets properly
env:
  SECRET_KEY: ${{ secrets.SECRET_KEY }}
```

## **Tools and Extensions**

### **VS Code Extensions**
- **YAML Language Support**: Built-in YAML support
- **GitHub Actions**: GitHub Actions workflow IntelliSense
- **YAML Schema Validator**: Schema validation

### **Command Line Tools**
- **yamllint**: YAML linting tool
- **yq**: YAML processor and validator
- **actionlint**: GitHub Actions workflow linter

## **References**
- **YAML 1.2.2**: [Official YAML Specification](https://yaml.org/spec/1.2.2/)
- **GitHub Actions**: [Workflow Syntax](https://docs.github.com/en/actions/using-workflows/workflow-syntax-for-github-actions)
- ~~CodeCov YAML~~ (Deprecated): [CodeCov Configuration](https://docs.codecov.com/docs/codecov-yaml)
- **YAML Security**: [YAML Security Best Practices](https://blog.gitguardian.com/security-yaml-files/)

---
**Last Updated**: July 25, 2025
**YAML Version**: 1.2.2
**GitHub Actions**: Latest Schema
**YAML Version**: 1.2.2
**GitHub Actions**: Latest Schema
