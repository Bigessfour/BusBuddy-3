# BusBuddy Repository Fetchability Index

## Overview

This document provides a comprehensive index of all files in the BusBuddy repository with their raw GitHub URLs for AI assistant access. The index is automatically generated and contains both SHA-specific and branch-specific URLs for maximum compatibility.

## Index Structure

The fetchability index (`FETCHABILITY-INDEX-COMPLETE.json`) contains the following information for each file:

- **`url_sha`**: SHA-specific raw GitHub URL (permanent link to specific commit)
- **`url_branch`**: Branch-specific raw GitHub URL (follows branch updates)
- **`type`**: File extension/type
- **`path`**: Relative path within the repository
- **`category`**: Functional categorization

## File Categories

### Source Code (`source_code`)

Core application logic, services, models, and implementation files.

**Key Directories:**

- `BusBuddy.Core/Models/` - Domain models and entities
- `BusBuddy.Core/Services/` - Business logic services
- `BusBuddy.WPF/Views/` - XAML views and code-behind
- `BusBuddy.WPF/ViewModels/` - MVVM view models
- `BusBuddy.Core/Data/Repositories/` - Data access layer

### Configuration (`configuration`)

Application settings, build configurations, and environment-specific files.

**Key Files:**

- `appsettings.json` - Application configuration
- `.vscode/settings.json` - VS Code workspace settings
- `Directory.Build.props` - Build configuration
- `global.json` - .NET SDK version specification

### Documentation (`documentation`)

README files, guides, and project documentation.

**Key Files:**

- `README.md` - Main project documentation
- `STRUCTURE-INDEX.md` - Repository structure guide
- `BusBuddy.Tests/TESTING-STANDARDS.md` - Testing guidelines
- Various `.md` files in `Documentation/` directory

### Tests (`tests`)

Unit tests, integration tests, and test configuration.

**Key Directories:**

- `BusBuddy.Tests/Core/` - Core service tests
- `BusBuddy.Tests/ViewModels/` - View model tests
- `BusBuddy.Tests/ValidationTests/` - Validation tests

### Build Artifacts (`build_artifacts`)

Generated files and build outputs.

**Key Files:**

- `BusBuddy.db` - SQLite database file
- Various `.xml` documentation files
- Build output files

### Other (`other`)

Miscellaneous files including project files, scripts, and assets.

**Key Files:**

- `.csproj` and `.sln` files - Project structure
- `.gitignore` and other configuration files
- Asset files and resources

## Usage for AI Assistants

### Accessing Files

AI assistants can use the URLs in this index to directly access file contents without requiring local workspace access. This is particularly useful for:

1. **Code Analysis**: Reading source code to understand implementation
2. **Documentation Review**: Accessing project documentation and guides
3. **Configuration Understanding**: Reviewing build and runtime configurations
4. **Test Review**: Understanding test coverage and implementation

### URL Types

#### SHA-Specific URLs (`url_sha`)

- **Format**: `https://raw.githubusercontent.com/{owner}/{repo}/{sha}/{path}`
- **Advantages**: Permanent links that never change
- **Use Case**: Referencing specific versions of files

#### Branch-Specific URLs (`url_branch`)

- **Format**: `https://raw.githubusercontent.com/{owner}/{repo}/{branch}/{path}`
- **Advantages**: Automatically updates with branch changes
- **Use Case**: Accessing latest versions on development branches

## Repository Statistics

Based on the current index:

- **Total Files**: 4,564 files indexed
- **Source Code Files**: Majority in C# (.cs) and XAML (.xaml)
- **Configuration Files**: JSON, XML, and various config formats
- **Documentation**: Markdown files and XML documentation
- **Test Coverage**: Comprehensive test suite with multiple categories

## Maintenance

### Automatic Generation

The fetchability index is generated using the `generate-fetchability-index.ps1` script located in the `tools/scripts/` directory.

### Validation

The `verify-fetchability.ps1` script can be used to validate the integrity of the index file.

### Update Process

1. Run `generate-fetchability-index.ps1` to update the index
2. Run `verify-fetchability.ps1` to validate the result
3. Commit the updated `FETCHABILITY-INDEX-COMPLETE.json`

## Integration with AI Tools

This fetchability index enables AI assistants to:

1. **Direct File Access**: Retrieve any file content using raw GitHub URLs
2. **Context-Aware Analysis**: Understand file relationships and categories
3. **Version Tracking**: Access both specific versions and latest versions
4. **Comprehensive Coverage**: Access all repository files, not just workspace files

## File Type Distribution

### Code Files

- **C# Files (.cs)**: Core application logic, services, view models, tests
- **XAML Files (.xaml)**: UI definitions and layouts
- **Project Files (.csproj, .sln)**: Build and project configuration

### Configuration Files

- **JSON Files (.json)**: Application settings, VS Code config, package references
- **XML Files (.xml)**: Build configurations, documentation, manifests
- **YAML Files (.yaml)**: CI/CD pipelines, linting configurations

### Documentation Files

- **Markdown Files (.md)**: Guides, READMEs, and documentation
- **Text Files (.txt)**: Data files, dictionaries, and plain text content

### Other Files

- **Database Files (.db)**: SQLite databases
- **PowerShell Scripts (.ps1)**: Build and utility scripts
- **Batch Files (.bat)**: Windows batch scripts
- **Resource Files**: Images, assets, and other resources

## Best Practices for AI Usage

1. **Use SHA URLs for Stability**: When referencing specific implementations
2. **Use Branch URLs for Latest**: When accessing current development versions
3. **Check File Categories**: Understand the context and purpose of files
4. **Validate URLs**: Ensure URLs are accessible before relying on them
5. **Cache Appropriately**: Consider caching frequently accessed files

## Troubleshooting

### Common Issues

- **404 Errors**: File may have been moved or deleted
- **Access Denied**: Repository may be private or require authentication
- **Outdated URLs**: Index may need regeneration after repository changes

### Validation Steps

1. Run `verify-fetchability.ps1` to check index integrity
2. Test sample URLs from different categories
3. Regenerate index if files are missing or URLs are invalid

## Related Files

- `FETCHABILITY-INDEX-COMPLETE.json` - The complete index file
- `tools/scripts/generate-fetchability-index.ps1` - Index generation script
- `tools/scripts/verify-fetchability.ps1` - Index validation script
- `STRUCTURE-INDEX.md` - Repository structure documentation

---

_This document is automatically maintained. Last updated: Generated from FETCHABILITY-INDEX-COMPLETE.json_</content>
<parameter name="filePath">c:\Users\biges\Desktop\BusBuddy\FILE-FETCHABILITY-GUIDE.md
