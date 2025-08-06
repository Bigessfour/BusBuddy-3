# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 1.0.x   | :white_check_mark: |

## Reporting a Vulnerability

If you discover a security vulnerability within BusBuddy, please send an email to the project maintainer. All security vulnerabilities will be promptly addressed.

**Please do not report security vulnerabilities through public GitHub issues.**

## Security Best Practices

### API Keys and Sensitive Data
- Never commit API keys, connection strings, or other sensitive data to the repository
- Use environment variables or Azure Key Vault for production secrets
- The `.gitignore` file is configured to exclude common secret patterns

### Database Security
- Use parameterized queries (Entity Framework Core handles this automatically)
- Implement proper authentication and authorization
- Use connection string encryption in production

### WPF Application Security
- Input validation is implemented at both UI and business logic layers
- SQL injection protection through Entity Framework Core
- Sensitive data logging is disabled by default in production builds

### Dependencies
- Regularly update NuGet packages to patch security vulnerabilities
- Monitor for security advisories on dependencies
- Use `dotnet list package --vulnerable` to check for known vulnerabilities

### Development Environment
- Keep development tools updated (Visual Studio, .NET SDK)
- Use secure development practices
- Enable static analysis tools

## Configuration Security

### appsettings.json
The following configuration files should never contain sensitive data:
- `appsettings.json` (committed to repository)
- `appsettings.test.json` (for testing only)

Use these patterns for sensitive configuration:
- `appsettings.Development.json` (local development, not committed)
- `appsettings.Production.json` (production secrets, not committed)
- Environment variables
- Azure Key Vault (recommended for production)

### Syncfusion License Key
- Store Syncfusion license key in environment variable `SYNCFUSION_LICENSE_KEY`
- Never commit license keys to the repository
- Use secure key management in production environments

## Code Review Guidelines
- All code changes should be reviewed before merging
- Pay special attention to data access patterns
- Verify that no sensitive information is being logged
- Ensure proper error handling that doesn't expose system internals
