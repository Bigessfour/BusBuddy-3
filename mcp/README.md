# BusBuddy MCP Workspace

A comprehensive Model Context Protocol (MCP) workspace providing enhanced AI capabilities for the BusBuddy transportation management system.

## 🚀 Quick Start

```bash
# Check system health
npm run health

# Start the enhanced Grok-4 server
npm run start:grok

# Test enhanced AI capabilities
npm run test:grok

# Stop all servers cleanly
npm run stop:all
```

## 📁 Folder Structure

```
mcp/
├── servers/           # Production MCP servers
│   ├── grok4-mcp-server.js        # Enhanced AI server (Quick/Expert modes)
│   ├── busbuddy-mcp-server.js     # BusBuddy operations server
│   └── git-mcp-server.js          # Git integration server
├── config/            # Configuration management
│   ├── server-configs.json        # Server metadata and settings
│   └── environment.json           # Environment variable templates
├── tools/             # Management utilities
│   ├── server-manager.js          # Server lifecycle management
│   └── validate-config.js         # Configuration validation
├── documentation/     # Documentation and guides
│   └── README.md                  # Tool documentation
└── archived/          # Test files and experiments
    ├── test-enhanced-mcp-evaluation.mjs  # Comprehensive testing
    └── demo-enhanced-mcp.mjs             # Simple demonstration
```

## 🧠 Enhanced AI Capabilities

### Grok-4 Integration

- **Quick Mode**: 1-2 actionable items, 400 tokens max
- **Expert Mode**: 2-3 detailed recommendations, 1200 tokens max
- **Structured Outputs**: JSON schema validation for consistent responses
- **Context Awareness**: 256K context window for comprehensive understanding

### Available Tools

1. **grok-analyze-problem**: Debug issues with controlled verbosity
2. **grok-code-review**: Code quality assessment with focused feedback
3. **grok-syncfusion-guidance**: UI component recommendations
4. **grok-azure-sql-optimize**: Database performance optimization
5. **grok-architecture-review**: System design evaluation

## 🛠️ Server Management

### Using the Server Manager

```bash
# Start specific server
node tools/server-manager.js start grok4-mcp-server

# Check all server health
node tools/server-manager.js health

# List available servers
node tools/server-manager.js list

# Stop all servers
node tools/server-manager.js stop-all
```

### NPM Script Shortcuts

```bash
npm run manager health      # Check system status
npm run start:grok          # Start Grok-4 server
npm run start:busbuddy      # Start BusBuddy server
npm run stop:all            # Clean shutdown
```

## 🔧 Configuration

### Environment Variables

```bash
# Required for Grok-4 server
XAI_API_KEY=your_xai_api_key_here

# Optional for enhanced features
NODE_ENV=development
MCP_LOG_LEVEL=info
```

### VS Code Integration

The MCP servers integrate with VS Code through the `.vscode/mcp.json` configuration:

```json
{
    "mcpServers": {
        "grok4-enhanced": {
            "command": "node",
            "args": ["mcp/servers/grok4-mcp-server.js"],
            "env": {
                "XAI_API_KEY": "${env:XAI_API_KEY}"
            }
        }
    }
}
```

## 🧪 Testing & Validation

### Comprehensive Testing

```bash
# Run full MCP evaluation suite
npm run test:grok

# Quick demonstration
npm run demo:grok

# Validate configurations
npm run validate:config
```

### Test Coverage

- Quick vs Expert mode validation
- Response schema compliance
- Error handling and recovery
- Tool parameter validation
- Performance benchmarking

## 📊 Features

### Response Control

- **Schema Validation**: Prevents verbose AI responses
- **Mode Selection**: Quick (concise) vs Expert (detailed)
- **Token Limits**: Enforced maximum response lengths
- **Temperature Control**: Balanced creativity vs consistency

### Process Management

- **Health Monitoring**: Server status and environment validation
- **Graceful Shutdown**: Clean process termination
- **Error Recovery**: Automatic restart capabilities
- **Output Logging**: Centralized server log management

### Integration Ready

- **VS Code MCP**: Direct integration with editor
- **BusBuddy Context**: Transportation domain expertise
- **Azure SQL**: Database optimization capabilities
- **Syncfusion UI**: Component guidance and best practices

## 🔄 Development Workflow

1. **Start Development**: `npm run health` to verify setup
2. **Launch Servers**: `npm run start:grok` for AI capabilities
3. **Test Changes**: `npm run test:grok` for validation
4. **Clean Shutdown**: `npm run stop:all` when complete

## 📋 Requirements

- **Node.js**: Version 18.0.0 or higher
- **XAI API Key**: For Grok-4 integration
- **VS Code**: With MCP extension for client integration
- **Environment**: Windows/Linux/macOS compatible

## 🎯 Next Steps

1. Set up environment variables (`XAI_API_KEY`)
2. Run health check: `npm run health`
3. Test Quick mode: `npm run demo:grok`
4. Integrate with VS Code MCP configuration
5. Explore enhanced AI capabilities in development workflow
