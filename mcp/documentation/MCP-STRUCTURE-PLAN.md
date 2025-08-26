# BusBuddy MCP Structure

## 📁 Proper MCP Folder Organization

```
mcp/
├── servers/                    # Active MCP servers
│   ├── grok4-mcp-server.js    # Enhanced Grok-4 AI server
│   ├── busbuddy-mcp-server.js # Core BusBuddy operations
│   └── git-mcp-server.js      # Git operations
├── config/                     # Server configurations
│   ├── server-configs.json    # Server settings
│   └── environment.json       # Environment variables
├── tools/                      # MCP utilities
│   ├── server-manager.js      # Start/stop servers
│   └── test-client.js         # Testing utilities
├── documentation/              # MCP documentation
│   ├── README.md              # Main documentation
│   ├── server-api.md          # Server API reference
│   └── integration-guide.md   # Integration examples
├── archived/                   # Old/experimental files
│   └── [moved old test files]
├── package.json               # Node.js dependencies
└── package-lock.json         # Dependency lock file
```

## 🧹 Cleanup Actions

1. **Move experimental/test files to archived/**
2. **Keep only production servers in servers/**
3. **Organize configurations in config/**
4. **Create proper documentation structure**
5. **Clean up root directory**

## ✅ Production Servers

- **grok4-mcp-server.js**: Enhanced with structured outputs, Quick/Expert modes
- **busbuddy-mcp-server.js**: Core BusBuddy operations, PowerShell integration
- **git-mcp-server.js**: Git operations and repository management
