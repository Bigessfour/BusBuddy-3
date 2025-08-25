# BusBuddy Package Update Script
# Updates deprecated NuGet packages for .NET 9 compatibility

Write-Output "🔄 Updating deprecated NuGet packages for BusBuddy..."

# Set working directory
$workspaceRoot = "C:\Users\biges\Desktop\BusBuddy"
Set-Location -Path $workspaceRoot

try {
    # Update AutoMapper.Extensions.Microsoft.DependencyInjection from 12.0.1 to 13.0.0
    Write-Output "📦 Upgrading AutoMapper.Extensions.Microsoft.DependencyInjection to 13.0.0..."

    # Update in BusBuddy.WPF
    Write-Output "  └─ Updating BusBuddy.WPF..."
    & dotnet add BusBuddy.WPF/BusBuddy.WPF.csproj package AutoMapper.Extensions.Microsoft.DependencyInjection --version 13.0.0

    # Update NetTopologySuite.IO.ShapeFile to latest compatible version
    Write-Output "📦 Keeping NetTopologySuite.IO.ShapeFile at 2.1.0 (latest compatible version)..."

    # Note: NetTopologySuite.IO.Esri.Shapefile was tested but has API breaking changes
    # Sticking with the updated but compatible NetTopologySuite.IO.ShapeFile 2.1.0

    # Restore solution to ensure all packages are properly updated
    Write-Output "🔧 Restoring solution..."
    & dotnet restore BusBuddy.sln --verbosity minimal

    if ($LASTEXITCODE -eq 0) {
        Write-Output "✅ Package restore completed successfully"
    } else {
        Write-Error "❌ Package restore failed with exit code $LASTEXITCODE"
        exit 1
    }

    # Verify build with updated packages
    Write-Output "🏗️ Verifying build with updated packages..."
    & dotnet build BusBuddy.sln --configuration Release --no-restore --verbosity minimal

    if ($LASTEXITCODE -eq 0) {
        Write-Output "✅ Build verification successful with updated packages"
        Write-Output ""
        Write-Output "📋 Package update summary:"
        Write-Output "  • AutoMapper.Extensions.Microsoft.DependencyInjection: 12.0.1 → 13.0.0"
        Write-Output "  • NetTopologySuite.IO.ShapeFile: kept at 2.1.0 (latest compatible version)"
        Write-Output ""
        Write-Output "🎉 All package updates completed successfully!"
        Write-Output "🎉 All package updates completed successfully!"
    } else {
        Write-Error "❌ Build verification failed with exit code $LASTEXITCODE"
        Write-Output "This may indicate compatibility issues with the updated packages."
        exit 1
    }

} catch {
    Write-Error "❌ Package update failed: $($_.Exception.Message)"
    Write-Output "Current location: $(Get-Location)"
    Write-Output "Available .csproj files:"
    Get-ChildItem -Recurse -Filter "*.csproj" | Select-Object Name, DirectoryName
    exit 1
}
