# BusBuddy Core Docker Image for Testing (Linux .NET 9)
# Focus: Build and test BusBuddy.Core (data, services, logic, seeding).
# This sidesteps the Windows TFM limitation for WPF (BusBuddy.WPF requires Windows host or Windows containers).
# Great for: Reproducible Core tests, integration with DB via compose, CI for business logic.

FROM mcr.microsoft.com/dotnet/sdk:9.0

WORKDIR /app

COPY .dockerignore ./
COPY BusBuddy.sln ./
COPY Directory.Build.props ./
COPY Directory.Build.targets ./
COPY BusBuddy.Core/BusBuddy.Core.csproj ./BusBuddy.Core/

# Layered restore for Core (flag for the windows TFM refs that may appear via sln).
# A second restore after full COPY helps with analyzer/package cache issues seen in Docker on some hosts (long paths / layer NuGet state).
RUN dotnet restore BusBuddy.Core/BusBuddy.Core.csproj -p:EnableWindowsTargeting=true --verbosity minimal

COPY . .

# Re-restore after full context (fixes "package not found after restore" / analyzer resolution in practice on Mac Docker).
RUN dotnet restore BusBuddy.Core/BusBuddy.Core.csproj -p:EnableWindowsTargeting=true --verbosity minimal

# Build Core only (the supported part on Linux base). Full sln/WPF on Windows runner or VM.
RUN dotnet build BusBuddy.Core/BusBuddy.Core.csproj --configuration Release --no-restore -p:EnableWindowsTargeting=true -v minimal

# Default: Run any Core tests or a simple verification (extend CMD for full test runs on host for WPF)
# For real DB tests: Use with docker-compose Postgres service + update connection in tests.
CMD ["dotnet", "build", "BusBuddy.Core/BusBuddy.Core.csproj", "--configuration", "Release", "--no-restore", "-p:EnableWindowsTargeting=true", "-v", "minimal"]
