BusBuddyValidator
==================

Small .NET 9 console tool to validate the presence of the BusBuddy.Testing assembly in a Modules directory.

Usage:
- Build: dotnet build Tools/BusBuddyValidator/BusBuddyValidator.csproj
- Run: dotnet run --project Tools/BusBuddyValidator/BusBuddyValidator.csproj

Behavior:
- Writes `busbuddy-validator-result.json` next to the executable with details and instructions when the assembly is missing.
- Exit codes: 0 = success (assembly present), 2 = modules dir missing, 3 = assembly missing, 4 = unexpected error

Documentation link: ./SETUP-GUIDE.md
