# MCP Server Manager

A utility for managing BusBuddy MCP servers with start, stop, health monitoring, and configuration management.

## Quick Start

```bash
# Start the enhanced Grok-4 server
node tools/server-manager.js start grok4-mcp-server

# Check all server status
node tools/server-manager.js health

# List available servers
node tools/server-manager.js list

# Stop all servers
node tools/server-manager.js stop-all
```

## Commands

### `start <server-name>`
Starts a specific MCP server with proper process monitoring.

```bash
node tools/server-manager.js start grok4-mcp-server
node tools/server-manager.js start busbuddy-mcp-server
```

### `stop <server-name>`
Gracefully stops a running MCP server.

```bash
node tools/server-manager.js stop grok4-mcp-server
```

### `list`
Displays all available servers with their status and descriptions.

### `health`
Performs comprehensive health check including:
- Server running status
- Environment variable validation
- Configuration file integrity

### `stop-all`
Stops all running MCP servers for clean shutdown.

## Server Configuration

The manager reads from `config/server-configs.json` to understand:
- Available servers and their metadata
- Required environment variables
- Tool capabilities
- Version information

## Process Management

- **Auto-restart**: Servers are monitored and can be restarted if they crash
- **Graceful shutdown**: Uses SIGTERM for clean server termination
- **Output logging**: All server stdout/stderr is captured and labeled
- **Exit code monitoring**: Tracks server health through exit codes

## Environment Integration

Works with VS Code MCP configuration by:
1. Reading from centralized config files
2. Validating environment variables
3. Providing server status information
4. Managing server lifecycle

## Error Handling

- Validates server existence before starting
- Checks for required environment variables
- Provides clear error messages
- Handles configuration file issues gracefully

## Development Workflow

Ideal for:
- Testing MCP server changes
- Managing multiple server environments
- Debugging server integration issues
- Automating server deployment
