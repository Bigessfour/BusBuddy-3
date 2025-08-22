# 📋 JSON Standards for BusBuddy

## **Official JSON Standard**

- **Specification**: [RFC 8259](https://tools.ietf.org/html/rfc8259) - The JavaScript Object Notation (JSON) Data Interchange Format
- **Schema Standard**: [JSON Schema Draft 2020-12](https://json-schema.org/draft/2020-12/schema)
- **Official Website**: [JSON.org](https://www.json.org/)

## **JSON Usage in BusBuddy**

### **Configuration Files**

- `appsettings.json` - Application configuration
- `appsettings.azure.json` - Azure-specific settings
- `global.json` - .NET SDK version specification
- `packages.lock.json` - NuGet package lock files

### **Performance Data**

- `benchmark-results-*.json` - Performance benchmark results

## **JSON Standards Enforcement**

### ✅ **Required JSON Standards**

#### **1. Syntax Rules (RFC 8259)**

```json
{
    "stringValue": "Must use double quotes",
    "numberValue": 42,
    "booleanValue": true,
    "nullValue": null,
    "arrayValue": [1, 2, 3],
    "objectValue": {
        "nestedProperty": "value"
    }
}
```

#### **2. Naming Conventions**

- **Property Names**: Use `camelCase` for consistency with .NET conventions
- **Boolean Properties**: Use descriptive names (`isEnabled`, `hasPermission`)
- **Array Properties**: Use plural nouns (`items`, `configurations`)
- **Nested Objects**: Use descriptive object names

#### **3. Structure Standards**

```json
{
    "metadata": {
        "version": "1.0.0",
        "created": "2025-07-25T00:00:00Z",
        "description": "Configuration description"
    },
    "configuration": {
        "setting1": "value1",
        "setting2": {
            "nestedSetting": "value"
        }
    }
}
```

#### **4. Data Types**

- **Strings**: Always use double quotes, escape special characters
- **Numbers**: Use appropriate numeric types (integer/decimal)
- **Booleans**: Use `true`/`false` (lowercase)
- **Null Values**: Use `null` (lowercase)
- **Arrays**: Consistent element types preferred
- **Objects**: Use meaningful property hierarchies

#### **5. Comments and Documentation**

```json
{
    "_comment": "JSON doesn't support comments, use _comment property for documentation",
    "_description": "This file contains application configuration settings",
    "actualConfiguration": {
        "setting": "value"
    }
}
```

### ✅ **Security Standards**

#### **1. Sensitive Data**

- ❌ **Never store passwords in plain text**
- ❌ **Never commit API keys or secrets**
- ✅ **Use environment variable placeholders**: `"${ENVIRONMENT_VARIABLE}"`
- ✅ **Use configuration providers for secrets**

#### **2. Input Validation**

- **Schema Validation**: Validate against JSON Schema when applicable
- **Type Checking**: Ensure proper data types for all properties
- **Range Validation**: Validate numeric ranges and string lengths

### ✅ **Performance Standards**

#### **1. File Size**

- **Configuration Files**: Keep under 1MB
- **Data Files**: Consider chunking for files over 10MB
- **Avoid Deep Nesting**: Limit to 5-7 levels deep

#### **2. Parsing Optimization**

- **Use `System.Text.Json`** for .NET 8.0 applications
- **Avoid Reflection**: Use source generators when possible
- **Streaming**: Use streaming APIs for large files

## **BusBuddy-Specific JSON Patterns**

### **Configuration Template**

```json
{
    "_metadata": {
        "schema": "https://json-schema.org/draft/2020-12/schema",
        "version": "1.0.0",
        "description": "BusBuddy configuration schema"
    },
    "connectionStrings": {
        "defaultConnection": "${CONNECTION_STRING}",
        "busbuddyDatabase": "${DATABASE_CONNECTION}"
    },
    "logging": {
        "logLevel": {
            "default": "Information",
            "microsoft": "Warning"
        }
    },
    "features": {
        "enableGoogleEarth": true,
        "enableAdvancedReporting": false
    }
}
```

### **API Response Template**

```json
{
    "success": true,
    "timestamp": "2025-07-25T12:00:00Z",
    "data": {
        "drivers": [
            {
                "id": 1,
                "name": "John Doe",
                "licenseNumber": "DL123456",
                "isActive": true
            }
        ]
    },
    "metadata": {
        "totalCount": 1,
        "pageSize": 10,
        "currentPage": 1
    },
    "errors": []
}
```

## **Validation Commands**

```powershell
# Validate JSON syntax using PowerShell
Test-Json -Json (Get-Content "appsettings.json" -Raw)

# Format JSON files consistently
Get-Content "file.json" | ConvertFrom-Json | ConvertTo-Json -Depth 10 | Set-Content "file.json"

# Validate against schema (if schema available)
# Install: npm install -g jsonschema
# jsonschema -i appsettings.json schema.json
```

## **Tools and Extensions**

### **VS Code Extensions**

- **JSON Language Features**: Built-in JSON support
- **JSON Schema**: Schema validation and IntelliSense
- **Prettier**: JSON formatting

### **.NET Tools**

- **System.Text.Json**: High-performance JSON serialization
- **Newtonsoft.Json**: Feature-rich JSON library (legacy support)
- **JSON Schema Validator**: Schema validation tools

## **References**

- **RFC 8259**: [JSON Data Interchange Format](https://tools.ietf.org/html/rfc8259)
- **JSON Schema**: [JSON Schema Specification](https://json-schema.org/)
- **System.Text.Json**: [.NET JSON APIs](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json-overview)
- **Security Guidelines**: [JSON Security Best Practices](https://cheatsheetseries.owasp.org/cheatsheets/JSON_Security_Cheat_Sheet.html)

---

**Last Updated**: July 25, 2025
**Standard Version**: RFC 8259
**Schema Version**: Draft 2020-12
