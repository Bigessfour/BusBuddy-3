# GitKraken Pro Integration Guide for BusBuddy

This guide provides step-by-step instructions for setting up GitKraken Pro with your BusBuddy project, integrated with the existing PowerShell automation system.

## 🚀 Quick Start

After setting up GitKraken Pro, you can immediately use these integrated commands:

```powershell
# Load BusBuddy environment (run once per session)
. .\PowerShell\Profiles\Microsoft.PowerShell_profile.ps1

# GitKraken integration commands (bb-prefixed for consistency)
bbGitKraken          # Launch GitKraken Desktop with BusBuddy repo
bbGkStart            # Same as above (alias)
bbGkWorkflow Status  # Check repository status with enhanced GitKraken features
bbGkBranch "azure-sql-integration"  # Create a new feature branch
bbGkHelp             # Show comprehensive GitKraken help
```

## 📋 Installation and Setup

### 1. Install GitKraken Pro

1. **Download**: Visit [GitKraken Desktop](https://help.gitkraken.com/gitkraken-desktop/gitkraken-desktop-home/)
2. **Install**: Run the installer and complete setup
3. **License**: Sign in and activate your Pro license
4. **Verify**: Ensure Pro features are enabled in `Preferences > General`

### 2. Authentication Setup

```powershell
# In GitKraken Desktop:
# 1. Go to Preferences > Authentication
# 2. Click "Connect to GitHub"
# 3. Authorize with OAuth for BusBuddy-3 repository
# 4. Add SSH key if needed via Preferences > SSH
```

### 3. Clone BusBuddy Repository

```powershell
# Option 1: Use GitKraken Desktop
# - Click + icon > Clone a repo > GitHub
# - Search for "Bigessfour/BusBuddy-3"
# - Clone to desired location

# Option 2: Use integrated command
bbGitKraken  # Opens GitKraken with current directory
```

### 4. Install GitKraken CLI (Optional but Recommended)

```powershell
# Install GitKraken CLI for enhanced automation
npm install -g @gitkraken/cli

# Verify installation
gk --version
```

## 🔧 BusBuddy-Specific Configuration

### Repository Setup for Syncfusion and Azure SQL

1. **Configure Git Ignore for WPF/Syncfusion**:
   ```
   /bin/
   /obj/
   *.user
   *.suo
   .vs/
   packages/
   TestResults/
   ```

2. **Environment Setup**:
   ```powershell
   # In your cloned repository
   dotnet restore BusBuddy.sln
   bbHealth  # Verify BusBuddy environment
   ```

3. **Create Development Branches**:
   ```powershell
   # Create feature branch for Syncfusion updates
   bbGkBranch "syncfusion-upgrade" -BranchType feature
   
   # Create branch for Azure SQL integration
   bbGkBranch "azure-sql-integration" -BranchType feature
   ```

## 🔄 Integrated Workflow

### Daily Development Workflow

```powershell
# 1. Start your session
. .\PowerShell\Profiles\Microsoft.PowerShell_profile.ps1
bbHealth

# 2. Open GitKraken and check status
bbGitKraken
bbGkWorkflow Status

# 3. Create feature branch if needed
bbGkBranch "my-feature" -BranchType feature

# 4. Development cycle
bbBuild    # Build project
bbTest     # Run tests
# Make your changes...

# 5. Commit and push (use GitKraken Desktop or CLI)
git add .
git commit -m "Implement feature X"
git push -u origin feature/my-feature

# 6. Create Pull Request
bbGkWorkflow CreatePR
```

### VS Code Integration

Use the new VS Code tasks for GitKraken workflows:

1. **GitKraken: Enhanced Workflow Manager** - Interactive menu for all GitKraken operations
2. **GitKraken: BusBuddy Development Workflow** - Complete development environment check
3. **GitKraken: Phase Analysis** - AI-powered analysis of project phases

Access via: `Ctrl+Shift+P` > `Tasks: Run Task`

## 🎯 Advanced Features

### Launchpad Integration

```powershell
# Open GitKraken Launchpad for project management
bbGkWorkflow LaunchLaunchpad

# Features available:
# - GitHub Issues integration
# - Pull Request management  
# - Workflow monitoring
# - Team collaboration
```

### Branch Management

```powershell
# Analyze current branch with AI
bbGkWorkflow BranchAnalysis

# Create branches following BusBuddy conventions
bbGkBranch "dashboard-enhancement" -BranchType feature
bbGkBranch "syncfusion-grid-fix" -BranchType bugfix
bbGkBranch "update-documentation" -BranchType docs
```

### CI/CD Monitoring

```powershell
# Monitor GitHub Actions workflows
bbGkWorkflow SetupCI

# Features include:
# - Real-time workflow status
# - Build/test monitoring
# - Deployment tracking
```

## 📚 Reference Documentation

### Project-Specific Resources
- **Repository**: https://github.com/Bigessfour/BusBuddy-3
- **Syncfusion WPF**: https://help.syncfusion.com/wpf/welcome-to-syncfusion-essential-wpf
- **Azure SQL**: https://learn.microsoft.com/en-us/azure/azure-sql/?view=azuresql
- **GitKraken Launchpad**: https://gitkraken.dev/launchpad/personal?groupBy=none&prs=github&issues=github

### GitKraken Resources
- **GitKraken Desktop Help**: https://help.gitkraken.com/gitkraken-desktop/gitkraken-desktop-home/
- **GitKraken CLI Documentation**: https://help.gitkraken.com/cli/cli-home/
- **GitKraken Pro Features**: https://help.gitkraken.com/gitkraken-desktop/pro-features/

## 🛠️ Troubleshooting

### Common Issues

1. **GitKraken Desktop not found**:
   ```powershell
   # Verify installation paths
   Get-ChildItem "$env:LOCALAPPDATA\GitKraken" -ErrorAction SilentlyContinue
   Get-ChildItem "$env:PROGRAMFILES\GitKraken" -ErrorAction SilentlyContinue
   ```

2. **Authentication issues**:
   - Regenerate SSH keys in GitKraken: `Preferences > SSH`
   - Reconnect GitHub integration: `Preferences > Authentication`

3. **Repository not recognized**:
   ```powershell
   # Verify you're in the correct directory
   Get-Location
   Test-Path "BusBuddy.sln"
   ```

4. **CLI commands not working**:
   ```powershell
   # Check GitKraken CLI installation
   npm list -g @gitkraken/cli
   
   # Reinstall if needed
   npm install -g @gitkraken/cli
   ```

### Getting Help

```powershell
# Show comprehensive help
bbGkHelp

# Get help for specific workflows
bbGkWorkflow --help

# Access BusBuddy command help
bbCommands
```

## 🎯 Integration with BusBuddy Finish Line

This GitKraken integration supports your Phase 2 goals:

- **Student Management Module**: Use feature branches for CRUD operations development
- **Vehicle & Driver Management**: Track maintenance calendar implementations
- **Route & Schedule Assignment**: Collaborate on route builder with SfMap
- **Activity & Compliance Logging**: Version control compliance reports
- **Dashboard & Navigation**: Manage DockingManager implementations
- **Data & Security Layer**: Handle Azure SQL migrations and EF Core updates

All GitKraken workflows integrate seamlessly with your existing `bb*` command system for a unified development experience.

---

**Last Updated**: August 17, 2025  
**Integration Version**: 1.0.0  
**PowerShell**: 7.5.2+  
**GitKraken**: Pro version recommended
