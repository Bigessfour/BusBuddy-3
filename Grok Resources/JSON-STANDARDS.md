# 🔧 JSON Standards for BusBuddy

## 📋 **Overview**
This document defines the JSON standards for the BusBuddy project, ensuring consistency, readability, and maintainability across all JSON files.

## 🎯 **General JSON Standards**

### **Formatting**
- **Indentation**: 2 spaces (no tabs)
- **Line Endings**: LF (Unix-style)
- **Encoding**: UTF-8
- **File Extension**: `.json`

### **Structure**
- **Root Level**: Always an object `{}` unless specifically an array
- **Property Ordering**: Alphabetical where possible
- **Comments**: Not supported in JSON (use `_comment` properties if needed)

## 📁 **File-Specific Standards**

### **Configuration Files**

#### **appsettings.json**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=BusBuddy.db"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    }
  }
}
```

#### **global.json**
```json
{
  "sdk": {
    "version": "9.0.303",
    "rollForward": "latestMinor"
  }
}
```

#### **package.json** (Node.js dependencies)
```json
{
  "name": "busbuddy-mcp-servers",
  "version": "1.0.0",
  "description": "Model Context Protocol servers for BusBuddy",
  "main": "index.js",
  "scripts": {
    "start": "node git-mcp-server.js",
    "test": "echo \"Error: no test specified\" && exit 1"
  },
  "dependencies": {
    "simple-git": "^3.19.1"
  }
}
```

### **MCP Configuration Files**

#### **mcp.json**
```json
{
  "mcpServers": {
    "git": {
      "command": "node",
      "args": ["mcp-servers/git-mcp-server.js"],
      "env": {
        "GIT_REPO_PATH": "."
      }
    },
    "filesystem": {
      "command": "node", 
      "args": ["mcp-servers/filesystem-mcp-server.js"],
      "env": {
        "ALLOWED_DIRECTORIES": "."
      }
    }
  }
}
```

## 🔧 **Development Standards**

### **Property Naming**
- **camelCase**: For property names
- **PascalCase**: For configuration section names (following .NET conventions)
- **Descriptive**: Use clear, descriptive property names

### **Value Standards**
```json
{
  "stringValue": "Use double quotes always",
  "numberValue": 42,
  "booleanValue": true,
  "nullValue": null,
  "arrayValue": [
    "item1",
    "item2"
  ],
  "objectValue": {
    "nestedProperty": "value"
  }
}
```

### **Connection Strings**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=BusBuddy.db",
    "AzureConnection": "Server=tcp:busbuddy.database.windows.net,1433;Database=BusBuddy;",
    "TestConnection": "Data Source=:memory:"
  }
}
```

## 🛡️ **Security Standards**

### **Sensitive Data**
- **No Secrets**: Never include passwords, API keys, or tokens
- **Environment Variables**: Reference sensitive data via environment variables
- **Placeholders**: Use placeholder values in committed files

```json
{
  "ApiSettings": {
    "BaseUrl": "https://api.busbuddy.com",
    "ApiKey": "${API_KEY}",
    "Timeout": 30000
  }
}
```

### **Configuration Overrides**
```json
{
  "Environment": "Development",
  "Features": {
    "EnableDebugMode": true,
    "EnableDetailedErrors": true,
    "EnableSensitiveDataLogging": false
  }
}
```

## 📊 **Validation Standards**

### **Schema Validation**
- **Required Properties**: Mark required properties clearly
- **Type Validation**: Ensure correct data types
- **Format Validation**: Validate formats (URLs, emails, etc.)

### **Example Schema Documentation**
```json
{
  "_schema": {
    "description": "BusBuddy application configuration",
    "required": ["ConnectionStrings", "Logging"],
    "properties": {
      "ConnectionStrings": {
        "type": "object",
        "required": ["DefaultConnection"]
      }
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=BusBuddy.db"
  }
}
```

## 🔍 **Linting and Formatting**

### **JSON Formatting Rules**
- **No trailing commas**: JSON does not support trailing commas
- **Consistent indentation**: Always 2 spaces
- **Property quotes**: Always use double quotes for property names
- **String quotes**: Always use double quotes for string values

### **VS Code Settings**
```json
{
  "editor.tabSize": 2,
  "editor.insertSpaces": true,
  "editor.formatOnSave": true,
  "json.format.keepLines": false
}
```

## 📁 **File Organization**

### **Configuration Structure**
```
BusBuddy/
├── appsettings.json                 # Main application configuration
├── appsettings.Development.json     # Development overrides
├── appsettings.Production.json      # Production overrides
├── global.json                      # .NET SDK configuration
├── package.json                     # Node.js dependencies
├── mcp.json                         # MCP server configuration
└── BusBuddy.WPF/
    └── appsettings.json            # WPF-specific configuration
```

### **Naming Conventions**
- **Configuration**: `appsettings.{Environment}.json`
- **Package Management**: `package.json`
- **Global Settings**: `global.json`
- **Custom Config**: `{purpose}.json` (e.g., `mcp.json`)

## ✅ **Quality Checklist**

### **Before Committing JSON Files**
- [ ] Valid JSON syntax (no syntax errors)
- [ ] Consistent 2-space indentation
- [ ] No trailing commas
- [ ] Double quotes for all strings and properties
- [ ] No sensitive data (passwords, tokens, keys)
- [ ] Logical property ordering
- [ ] Appropriate file naming
- [ ] Environment-specific configurations separated

### **Code Review Checklist**
- [ ] JSON validates against any defined schemas
- [ ] No hardcoded sensitive values
- [ ] Consistent formatting throughout
- [ ] Properties follow naming conventions
- [ ] Configuration is environment-appropriate
- [ ] No unnecessary complexity or nesting

## 🛠️ **Tools and Integration**

### **Recommended Tools**
- **VS Code**: Built-in JSON support with IntelliSense
- **JSON Schema**: For validation and auto-completion
- **Prettier**: For automatic formatting
- **ESLint**: For additional validation (if using Node.js)

### **Build Integration**
- **Validation**: JSON syntax validation in CI/CD pipeline
- **Schema Validation**: Automated schema checking
- **Security Scanning**: Check for potential secrets in JSON files

---

**Document Version**: 1.0  
**Last Updated**: July 30, 2025  
**Applies To**: All JSON files in BusBuddy project  
**Next Review**: Monthly standards review
