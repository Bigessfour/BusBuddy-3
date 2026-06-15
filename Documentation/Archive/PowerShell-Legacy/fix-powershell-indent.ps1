# PowerShell Indentation Fixer
$filePath = 'Powershell/Profiles/Microsoft.PowerShell_profile_optimized.ps1'
$content = Get-Content $filePath
$fixedContent = @()
$indentLevel = 0

foreach ($line in $content) {
    $trimmedLine = $line.TrimStart()

    # Handle region markers
    if ($trimmedLine -match '^#region') {
        $fixedContent += (' ' * ($indentLevel * 4)) + $trimmedLine
        continue
    }
    elseif ($trimmedLine -match '^#endregion') {
        $fixedContent += (' ' * ($indentLevel * 4)) + $trimmedLine
        continue
    }

    # Decrease indent for closing braces
    if ($trimmedLine -match '^}') {
        $indentLevel = [Math]::Max(0, $indentLevel - 1)
    }

    # Apply current indentation
    if ($trimmedLine -ne '') {
        $fixedContent += (' ' * ($indentLevel * 4)) + $trimmedLine
    } else {
        $fixedContent += ''
    }

    # Increase indent for opening braces and function definitions
    if ($trimmedLine -match '\{$' -or $trimmedLine -match '^function ' -or $trimmedLine -match '^if \(' -or $trimmedLine -match '^foreach ' -or $trimmedLine -match '^try$') {
        $indentLevel++
    }
}

# Write back to file
Set-Content -Path $filePath -Value $fixedContent -Encoding UTF8
Write-Information "Fixing PowerShell indentation for files under $Path" -InformationAction Continue
