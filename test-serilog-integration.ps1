# Test script for Serilog integration in PowerShell profile
$BusBuddyRepoPath = "c:\Users\biges\Desktop\BusBuddy"
$script:ProfileLogger = $null

function Initialize-BusBuddyProfileLogger {
    [CmdletBinding()]
    param()
    
    # Skip if already initialized
    if ($script:ProfileLogger) { return $script:ProfileLogger }
    
    try {
        # Check for Serilog module availability (lazy-loading)
        if (-not (Get-Module Serilog -ListAvailable -ErrorAction SilentlyContinue)) {
            Write-Verbose "Serilog module not available - profile logging disabled"
            return $null
        }
        
        # Import Serilog module only when needed
        Import-Module Serilog -Force -ErrorAction Stop
        
        # Configure logger for profile events with structured output
        $logPath = Join-Path $BusBuddyRepoPath "logs\profile.log"
        $loggerConfig = [Serilog.LoggerConfiguration]::new()
        $loggerConfig = $loggerConfig.MinimumLevel.Information()
        $loggerConfig = $loggerConfig.WriteTo.File($logPath, 
            [Serilog.Events.LogEventLevel]::Information,
            "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
            $null, 1048576, 31, $false, $false, $null, [System.Text.Encoding]::UTF8)
        $loggerConfig = $loggerConfig.WriteTo.Console([Serilog.Events.LogEventLevel]::Warning)
        $script:ProfileLogger = $loggerConfig.CreateLogger()
            
        Write-Verbose "✅ Profile logger initialized: $logPath"
        return $script:ProfileLogger
    }
    catch {
        Write-Verbose "⚠️ Failed to initialize profile logger: $($_.Exception.Message)"
        return $null
    }
}

function Write-ProfileLog {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Message,
        
        [ValidateSet('Information', 'Warning', 'Error')]
        [string]$Level = 'Information'
    )
    
    # Initialize logger on first use (lazy loading)
    if (-not $script:ProfileLogger) {
        $script:ProfileLogger = Initialize-BusBuddyProfileLogger
    }
    
    # Fall back to Write-Information/Write-Warning if Serilog unavailable
    if (-not $script:ProfileLogger) {
        switch ($Level) {
            'Information' { Write-Information $Message -InformationAction Continue }
            'Warning' { Write-Warning $Message }
            'Error' { Write-Error $Message }
        }
        return
    }
    
    # Log via Serilog with structured format
    switch ($Level) {
        'Information' { $script:ProfileLogger.Information($Message) }
        'Warning' { $script:ProfileLogger.Warning($Message) }
        'Error' { $script:ProfileLogger.Error($Message) }
    }
}

# Test the functions
Write-Host "Testing Serilog integration..."
Write-ProfileLog "Test information message" -Level Information
Write-ProfileLog "Test warning message" -Level Warning
Write-Host "Test completed successfully!"
