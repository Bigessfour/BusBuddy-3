# 🔧 YAML Standards for BusBuddy

## 📋 **Overview**
This document defines the YAML standards for the BusBuddy project, primarily covering GitHub Actions workflows and CI/CD pipeline configurations.

## 🎯 **General YAML Standards**

### **Formatting**
- **Indentation**: 2 spaces (no tabs)
- **Line Endings**: LF (Unix-style)
- **Encoding**: UTF-8
- **File Extension**: `.yml` (preferred) or `.yaml`

### **Structure**
- **Document Start**: Use `---` for explicit document start (optional for single documents)
- **Key-Value Pairs**: Use consistent spacing around colons
- **Arrays**: Use consistent dash formatting
- **Comments**: Use `#` for documentation

## 📁 **File-Specific Standards**

### **GitHub Actions Workflows**

#### **CI/CD Pipeline Example (.github/workflows/ci.yml)**
```yaml
name: 🚌 BusBuddy CI Pipeline

on:
  push:
    branches: [master, main, develop]
  pull_request:
    branches: [master, main]
  workflow_dispatch:
    inputs:
      debug_enabled:
        type: boolean
        description: "Enable debug mode for troubleshooting"
        default: false

env:
  DOTNET_VERSION: "9.0.x"
  SOLUTION_FILE: "BusBuddy.sln"
  BUILD_CONFIGURATION: "Release"

jobs:
  build-and-test:
    name: 🏗️ Build & Test
    runs-on: windows-latest
    timeout-minutes: 30
    
    strategy:
      matrix:
        platform: [x64, x86]
      fail-fast: false

    steps:
      - name: 📥 Checkout code
        uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: 🏗️ Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}
          cache: true

      - name: 📦 Restore dependencies
        run: dotnet restore ${{ env.SOLUTION_FILE }}

      - name: 🔨 Build solution
        run: |
          dotnet build ${{ env.SOLUTION_FILE }} \
            --configuration ${{ env.BUILD_CONFIGURATION }} \
            --no-restore \
            --verbosity normal

      - name: 🧪 Run tests
        run: |
          dotnet test ${{ env.SOLUTION_FILE }} \
            --configuration ${{ env.BUILD_CONFIGURATION }} \
            --no-build \
            --verbosity normal \
            --logger trx \
            --collect:"XPlat Code Coverage"
```

#### **Security Scanning Workflow**
```yaml
name: 🔒 Security Scan

on:
  push:
    branches: [main]
  schedule:
    - cron: '0 2 * * 1'  # Weekly on Monday at 2 AM UTC

jobs:
  security-scan:
    name: 🔍 Security Analysis
    runs-on: ubuntu-latest
    permissions:
      security-events: write
      actions: read
      contents: read

    steps:
      - name: 📥 Checkout code
        uses: actions/checkout@v4

      - name: 🔍 Run dependency scan
        run: |
          dotnet list package --vulnerable --include-transitive
          
      - name: 🔎 Scan for secrets
        run: |
          # Custom secret scanning logic
          echo "🔍 Scanning for potential secrets..."
          
          patterns=(
            'password\s*=\s*"[^"]*"'
            'api[_-]?key\s*=\s*"[^"]*"'
            'secret\s*=\s*"[^"]*"'
            'token\s*=\s*"[^"]*"'
          )
          
          for pattern in "${patterns[@]}"; do
            if grep -r -E "$pattern" . --exclude-dir=.git; then
              echo "⚠️ Potential secret found: $pattern"
            fi
          done
```

### **Docker Configuration (docker-compose.yml)**
```yaml
version: '3.8'

services:
  busbuddy-app:
    build:
      context: .
      dockerfile: Dockerfile
    ports:
      - "8080:80"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__DefaultConnection=Server=db;Database=BusBuddy;User=sa;Password=${SA_PASSWORD}
    depends_on:
      - db
    volumes:
      - ./logs:/app/logs

  db:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=${SA_PASSWORD}
      - MSSQL_DATABASE=BusBuddy
    ports:
      - "1433:1433"
    volumes:
      - mssql_data:/var/opt/mssql

volumes:
  mssql_data:
```

## 🔧 **YAML Syntax Standards**

### **Key-Value Pairs**
```yaml
# ✅ Correct formatting
key: value
another_key: "quoted value"
numeric_key: 42
boolean_key: true

# ❌ Incorrect formatting
key:value           # Missing space after colon
key : value         # Extra space before colon
```

### **Arrays and Lists**
```yaml
# ✅ Preferred array format
branches:
  - main
  - develop
  - feature/*

# ✅ Inline array format (for short lists)
platforms: [x64, x86, arm64]

# ❌ Inconsistent formatting
branches:
- main              # Inconsistent indentation
  - develop
```

### **Multi-line Strings**
```yaml
# ✅ Literal block scalar (preserves line breaks)
script: |
  echo "Starting build process..."
  dotnet build --configuration Release
  echo "Build completed successfully"

# ✅ Folded block scalar (joins lines)
description: >
  This is a long description that will be
  folded into a single line with spaces
  replacing the line breaks.

# ✅ Quoted multi-line string
command: "dotnet build --configuration Release --verbosity normal"
```

### **Comments and Documentation**
```yaml
# Main configuration for BusBuddy CI/CD pipeline
name: 🚌 BusBuddy CI Pipeline

# Trigger on pushes to main branches and pull requests
on:
  push:
    branches: [main, develop]  # Include development branches
  pull_request:
    branches: [main]           # Only PR target to main

# Global environment variables
env:
  DOTNET_VERSION: "9.0.x"     # Latest .NET 9 version
  BUILD_CONFIG: "Release"     # Always build in release mode
```

## 🛡️ **Security Standards**

### **Secrets Management**
```yaml
# ✅ Use GitHub secrets for sensitive data
env:
  CONNECTION_STRING: ${{ secrets.CONNECTION_STRING }}
  API_KEY: ${{ secrets.API_KEY }}

# ❌ Never hardcode secrets
env:
  CONNECTION_STRING: "Server=prod;Password=secret123;"  # DON'T DO THIS
```

### **Environment Variables**
```yaml
# ✅ Use environment variables for configuration
environment:
  - ASPNETCORE_ENVIRONMENT=Production
  - DATABASE_URL=${{ secrets.DATABASE_URL }}
  - FEATURE_FLAGS=${{ vars.FEATURE_FLAGS }}

# ✅ Conditional environment settings
environment:
  ASPNETCORE_ENVIRONMENT: ${{ github.ref == 'refs/heads/main' && 'Production' || 'Development' }}
```

## 📊 **Workflow Organization Standards**

### **Job Naming Convention**
```yaml
jobs:
  # Use descriptive names with emojis for visual clarity
  build-and-test:
    name: 🏗️ Build & Test
    
  security-scan:
    name: 🔒 Security Scan
    
  deploy-staging:
    name: 🚀 Deploy to Staging
    
  deploy-production:
    name: 🎯 Deploy to Production
```

### **Step Organization**
```yaml
steps:
  # Group related setup steps
  - name: 📥 Checkout code
    uses: actions/checkout@v4
    
  - name: 🏗️ Setup .NET
    uses: actions/setup-dotnet@v4
    with:
      dotnet-version: ${{ env.DOTNET_VERSION }}
      
  # Separate build steps
  - name: 📦 Restore dependencies
    run: dotnet restore
    
  - name: 🔨 Build solution
    run: dotnet build --no-restore
    
  # Test and validation steps
  - name: 🧪 Run tests
    run: dotnet test --no-build
```

### **Conditional Execution**
```yaml
# ✅ Environment-based conditions
- name: 🚀 Deploy to Production
  if: github.ref == 'refs/heads/main' && github.event_name == 'push'
  run: echo "Deploying to production..."

# ✅ Matrix-based conditions
- name: 🪟 Windows-specific setup
  if: matrix.os == 'windows-latest'
  run: echo "Setting up Windows environment..."

# ✅ Status-based conditions
- name: 📢 Notify on failure
  if: failure()
  run: echo "Build failed, sending notification..."
```

## 🔍 **Validation Standards**

### **YAML Lint Rules**
- **Indentation**: Consistent 2-space indentation
- **Line Length**: Maximum 120 characters per line
- **Trailing Spaces**: No trailing whitespace
- **Empty Lines**: Maximum 2 consecutive empty lines
- **Document Start**: Optional `---` for single documents

### **GitHub Actions Validation**
```yaml
# ✅ Validate action versions
- uses: actions/checkout@v4        # Use specific version
- uses: actions/setup-dotnet@v4    # Use latest major version

# ❌ Avoid floating tags
- uses: actions/checkout@main      # Don't use branch names
```

## 📁 **File Organization**

### **Workflow Directory Structure**
```
.github/
├── workflows/
│   ├── ci.yml                     # Main CI/CD pipeline
│   ├── security-scan.yml          # Security scanning
│   ├── release.yml                # Release management
│   ├── performance-test.yml       # Performance testing
│   └── cleanup.yml                # Maintenance tasks
├── ISSUE_TEMPLATE/
│   ├── bug_report.yml
│   └── feature_request.yml
└── pull_request_template.md
```

### **Naming Conventions**
- **Workflows**: `{purpose}.yml` (e.g., `ci.yml`, `security-scan.yml`)
- **Reusable Workflows**: `{purpose}-reusable.yml`
- **Templates**: `{type}_template.yml`

## ✅ **Quality Checklist**

### **Before Committing YAML Files**
- [ ] Valid YAML syntax (no parsing errors)
- [ ] Consistent 2-space indentation
- [ ] No trailing whitespace
- [ ] Meaningful comments for complex configurations
- [ ] No hardcoded secrets or sensitive data
- [ ] Action versions pinned to specific versions
- [ ] Descriptive job and step names
- [ ] Appropriate timeout values set

### **GitHub Actions Specific**
- [ ] Workflow triggers are appropriate
- [ ] Environment variables properly defined
- [ ] Secrets are used for sensitive data
- [ ] Matrix builds are properly configured
- [ ] Conditional logic is clear and tested
- [ ] Permissions are minimal and specific

## 🛠️ **Tools and Integration**

### **Recommended Tools**
- **VS Code**: YAML extension for validation and IntelliSense
- **yamllint**: Command-line YAML linter
- **GitHub Actions**: Built-in workflow validation
- **actionlint**: GitHub Actions specific linting

### **VS Code Configuration**
```yaml
# .vscode/settings.json
{
  "yaml.schemas": {
    "https://json.schemastore.org/github-workflow.json": ".github/workflows/*.yml"
  },
  "yaml.format.enable": true,
  "yaml.format.singleQuote": false
}
```

### **Linting Integration**
```yaml
# Example yamllint configuration (.yamllint)
rules:
  line-length:
    max: 120
  indentation:
    spaces: 2
  trailing-spaces: enable
  empty-lines:
    max: 2
```

---

**Document Version**: 1.0  
**Last Updated**: July 30, 2025  
**Applies To**: All YAML files in BusBuddy project  
**Next Review**: Monthly standards review
