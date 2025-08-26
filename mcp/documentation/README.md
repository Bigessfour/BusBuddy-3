# BusBuddy MCP Installation and Setup

## Prerequisites
- Node.js 18+ installed
- PowerShell 7.5.2 with bb-* commands available  
- BusBuddy project properly configured
- VS Code with MCP extension support

## Installation Steps

### 1. Install MCP SDK
```bash
cd mcp/
npm install
```

### 2. Environment Variables
Set these environment variables for proper operation:

```powershell
$env:BUSBUDDY_PROJECT_ROOT = "C:\path\to\BusBuddy"
$env:SYNCFUSION_LICENSE_KEY = "your-license-key"
$env:AZURE_SQL_CONNECTION = "your-connection-string"
```

### 3. VS Code Configuration
The MCP servers are configured in `.vscode/mcp.json`:

- **busbuddy-project**: Main BusBuddy operations
- **busbuddy-git**: Git integration  
- **azure-mcp**: Azure resource management
- **brave-search**: Web search with Syncfusion focus
- **microsoft-docs**: Official documentation search

### 4. Test Installation
```bash
# Test BusBuddy MCP server
node servers/busbuddy-mcp-server.js

# Test Git MCP server  
node servers/git-mcp-server.js
```

## Available Commands

### BusBuddy Project Commands
- `bb-health` - System health check
- `bb-build` - Build solution
- `bb-run` - Run WPF application
- `bb-test` - Run unit tests
- `busbuddy-db-query` - Database queries
- `busbuddy-logs` - Application logs
- `busbuddy-project-status` - Project overview
- `busbuddy-syncfusion-check` - Syncfusion status

### Git Commands
- `git-status` - Repository status
- `git-log` - Commit history
- `git-diff` - Show changes
- `git-branch` - Branch management
- `git-add` - Stage files
- `git-commit` - Commit changes

## Troubleshooting

### Server Not Starting
1. Check Node.js version: `node --version`
2. Verify MCP SDK installation: `npm list @modelcontextprotocol/sdk`
3. Check file paths in mcp.json
4. Verify environment variables

### PowerShell Commands Not Found
1. Ensure BusBuddy PowerShell profile is loaded
2. Check bb-* functions are available: `Get-Command bb-*`
3. Verify project root path is correct

### Database Connection Issues
1. Check Azure SQL connection string
2. Verify bb-sql-test command works
3. Check firewall settings for Azure SQL

## Security Notes

- Only SELECT queries allowed in database tools
- Git operations limited to safe commands
- Environment variables used for sensitive data
- MCP servers run with restricted permissions
