<# Hard Archived 2025-08-12: Duplicate of root Query-Students-Azure.ps1 kept canonical.
Original preserved in git history. Use root script at repository root.
#>
throw "Archived duplicate. Use root ./Query-Students-Azure.ps1"
        Write-Error ("sqlcmd failed (exit {0}): {1}" -f $sqlcmdExitCode, ($output -join ' '))
        exit $sqlcmdExitCode
    }

    # Emit raw output so callers can pipe/inspect
    $output | ForEach-Object { Write-Output $_ }

    # Quick count check (robust parse)
    $countQuery = 'SET NOCOUNT ON; SELECT COUNT(1) FROM Students;'
    $countLines = & sqlcmd -S $Server -d $Database -G -Q $countQuery -W -h -1 2>&1
    if ($LASTEXITCODE -eq 0 -and $countLines) {
        $countText = $countLines | Where-Object { $_ -match '^[0-9]+$' } | Select-Object -First 1
        if ($countText) {
            [int]$total = [int]$countText
            Write-Information ("Total students: {0}" -f $total) -InformationAction Continue
        }
    }
}
catch {
    Write-Error ("{0}" -f $_)
    exit 1
}
