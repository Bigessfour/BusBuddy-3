# Test-BusBuddyExecutable.ps1
# Diagnostic script to verify BusBuddy.WPF.exe generation

[CmdletBinding()]
param()

function Test-BusBuddyExecutable {
    <#
    .SYNOPSIS
    Verifies that BusBuddy.WPF.exe is properly generated after build

    .DESCRIPTION
    Checks for the existence of BusBuddy.WPF.exe in the expected output directory
    and provides diagnostic information if it's missing.

    .EXAMPLE
    Test-BusBuddyExecutable
    #>

    Write-Information "🔍 Checking BusBuddy executable generation..." -InformationAction Continue

    $exePath = "BusBuddy.WPF/bin/Debug/net9.0-windows/BusBuddy.WPF.exe"
    $fullPath = Join-Path $PWD $exePath

    if (Test-Path $fullPath) {
        $fileInfo = Get-Item $fullPath
        Write-Information "✅ Executable found: $exePath" -InformationAction Continue
        Write-Information "   📁 Size: $($fileInfo.Length) bytes" -InformationAction Continue
        Write-Information "   📅 Modified: $($fileInfo.LastWriteTime)" -InformationAction Continue

        # Test if it's a valid executable
        try {
            $fileVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($fullPath)
            Write-Information "   📝 File Version: $($fileVersion.FileVersion)" -InformationAction Continue
            Write-Information "   🏷️  Product: $($fileVersion.ProductName)" -InformationAction Continue
        }
        catch {
            Write-Warning "Could not read version information: $($_.Exception.Message)"
        }

        return $true
    }
    else {
        Write-Warning "❌ Executable not found: $exePath"
        Write-Information "🔧 Diagnostic steps:" -InformationAction Continue
        Write-Information "   1. Run 'dotnet clean BusBuddy.WPF/BusBuddy.WPF.csproj'" -InformationAction Continue
        Write-Information "   2. Run 'dotnet build BusBuddy.WPF/BusBuddy.WPF.csproj'" -InformationAction Continue
        Write-Information "   3. Check App.xaml.cs configuration" -InformationAction Continue
        Write-Information "   4. Verify OutputType=WinExe in .csproj" -InformationAction Continue

        # Check if the directory exists
        $outputDir = "BusBuddy.WPF/bin/Debug/net9.0-windows"
        if (Test-Path $outputDir) {
            Write-Information "📂 Output directory exists, checking contents:" -InformationAction Continue
            Get-ChildItem $outputDir -Name "BusBuddy.WPF.*" | ForEach-Object {
                Write-Information "   📄 Found: $_" -InformationAction Continue
            }
        }
        else {
            Write-Information "📂 Output directory does not exist: $outputDir" -InformationAction Continue
        }

        return $false
    }
}

# Export the function for module use
Export-ModuleMember -Function Test-BusBuddyExecutable

# Run immediately if script is executed directly
if ($MyInvocation.InvocationName -ne '.') {
    Test-BusBuddyExecutable
}
