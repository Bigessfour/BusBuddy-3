# 🔧 GitHub Workflow Enhancement Guide

This document demonstrates enhanced workflow capabilities and monitoring.

## 🚀 Advanced Workflow Features

### 1. **📊 Workflow Monitoring Commands**

Using Bus Buddy PowerShell tools to monitor workflows:

```powershell
# Monitor latest workflow runs
gh run list --limit 5

# Check specific workflow status
gh pr view <pr-number> --json statusCheckRollup

# Watch workflow in real-time
gh run watch <run-id>

# Get workflow logs
gh run view <run-id> --log-failed
```

### 2. **🔄 Workflow Triggers**

- **Push to main:** Triggers full CI/CD pipeline
- **Pull Request:** Triggers validation and testing
- **Manual Dispatch:** Can be triggered manually for testing
- **Tag Creation:** Triggers release workflow

### 3. **🛠️ Workflow Jobs Breakdown**

#### **Build & Test Jobs:**
- ✅ .NET Solution Compilation
- ✅ Unit Test Execution
- ✅ Test Result Reporting (.trx files)
- ✅ Build Artifact Generation

#### **Standards Validation Jobs:**
- ✅ JSON File Validation
- ✅ PowerShell Script Analysis
- ✅ Code Quality Checks
- ✅ Dependency Analysis

#### **Security & Health Jobs:**
- ✅ Vulnerability Scanning
- ✅ Secret Detection (GitGuardian)
- ✅ Repository Health Assessment
- ✅ License Compliance

## 🎯 Best Practices for PR Workflows

1. **Create Feature Branches:** Always work in feature branches
2. **Small, Focused Changes:** Keep PRs manageable and focused
3. **Clear Commit Messages:** Use conventional commit format
4. **Monitor Check Status:** Review all automated checks before merge
5. **Address Failures:** Fix any workflow failures before requesting review

## 📈 Workflow Performance Optimization

- **Parallel Execution:** Jobs run concurrently when possible
- **Caching:** Dependencies cached for faster builds
- **Conditional Execution:** Skip unnecessary jobs when possible
- **Matrix Builds:** Test across multiple configurations

## 🔍 Debugging Workflow Issues

```bash
# Check workflow status
gh workflow list

# View recent runs
gh run list --workflow="CI/CD"

# Download logs for analysis
gh run download <run-id>

# View specific job details
gh run view <run-id> --job="Build & Test"
```

---

*This guide shows advanced workflow monitoring and debugging capabilities.*
