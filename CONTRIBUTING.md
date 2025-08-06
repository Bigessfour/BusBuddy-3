# Contributing to BusBuddy

Thank you for your interest in contributing to BusBuddy! This document provides guidelines and information for contributors.

## Getting Started

### Prerequisites
- Visual Studio 2022 (Community, Professional, or Enterprise)
- .NET 9.0 SDK
- SQL Server (LocalDB, Express, or full version)
- Syncfusion WPF License (Community or Commercial)

### Development Setup
1. Clone the repository
2. Open `BusBuddy.sln` in Visual Studio
3. Set up your Syncfusion license key as an environment variable: `SYNCFUSION_LICENSE_KEY`
4. Restore NuGet packages
5. Build the solution
6. Run the application

## Development Guidelines

### Code Style
- Follow C# coding conventions
- Use meaningful variable and method names
- Add XML documentation for public APIs
- Keep methods focused and small
- Use MVVM pattern consistently in WPF code

### XAML Guidelines
- Follow the XML/XAML formatting rules in `.github/copilot-instructions.md`
- Use em dashes (—) instead of double dashes (--) in XML comments
- Ensure proper Syncfusion theme integration
- Use static resources appropriately

### Testing
- Write unit tests for business logic
- Add integration tests for data access
- Use NUnit framework with FluentAssertions
- Test Syncfusion components with proper UI thread handling
- See the `BusBuddy.Tests/` project for existing test examples

### Database Changes
- Use Entity Framework Core migrations for database changes
- Test migrations both up and down
- Ensure data integrity in migration scripts
- Update seed data if necessary

## Pull Request Process

1. **Fork and Branch**: Create a feature branch from `main`
2. **Develop**: Make your changes following the guidelines above
3. **Test**: Ensure all tests pass and add new tests for your changes
4. **Commit**: Use clear, descriptive commit messages
5. **Pull Request**: Submit a PR with a clear description of changes

### Commit Message Format
```
Type: Brief description

Detailed explanation if needed

Fixes #issue-number
```

Types: `feat`, `fix`, `docs`, `style`, `refactor`, `test`, `chore`

### Pull Request Checklist
- [ ] Code follows project style guidelines
- [ ] Self-review completed
- [ ] Tests added/updated and validated with NUnit
- [ ] Documentation updated if needed
- [ ] No sensitive data committed
- [ ] Syncfusion license compliance maintained

## Issue Guidelines

### Bug Reports
- Use the bug report template
- Include steps to reproduce
- Provide system information
- Include relevant logs/stack traces

### Feature Requests
- Use the feature request template
- Explain the use case
- Consider implementation complexity
- Align with project goals

### Syncfusion-Specific Issues
- Check Syncfusion documentation first
- Include Syncfusion component versions
- Verify license compliance
- Test with minimal reproduction case

## Architecture Guidelines

### WPF Layer (BusBuddy.WPF)
- Use MVVM pattern consistently
- Implement proper data binding
- Handle UI thread operations correctly
- Use Syncfusion controls appropriately

### Core Layer (BusBuddy.Core)
- Keep business logic UI-agnostic
- Use dependency injection
- Implement proper error handling
- Follow repository pattern

### Data Layer
- Use Entity Framework Core
- Implement proper transaction handling
- Use async/await patterns
- Handle concurrency conflicts

## Resources

- [Syncfusion WPF Documentation](https://help.syncfusion.com/wpf/welcome-to-syncfusion-essential-wpf)
- [.NET 8 Documentation](https://docs.microsoft.com/en-us/dotnet/)
- [Entity Framework Core Documentation](https://docs.microsoft.com/en-us/ef/core/)
- [WPF MVVM Guidelines](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/data/data-binding-overview)

## Questions?

If you have questions about contributing, please:
1. Check existing documentation
2. Search existing issues
3. Create a new issue with the question label

Thank you for contributing to BusBuddy!
