# BusBuddy Secure Configuration Module (Refactored to use Global Security Layer)
# Uses Global-SecureApiManager for enterprise-grade security

#requires -Version 7.5
#requires -Modules Microsoft.PowerShell.SecretManagement

[CmdletBinding()]
param()

# Import global security layer
$globalSecurityModule = Join-Path $PSScriptRoot "Global-SecureApiManager.psm1"
if (Test-Path $globalSecurityModule) {
    Import-Module $globalSecurityModule -Force -Global
    Write-Information "✅ Global security layer loaded" -InformationAction Continue
}
else {
    Write-Error "Global-SecureApiManager module not found. Cannot proceed without global security layer."
    return
}

function Initialize-SecureGrokConfig {
    <#
    .SYNOPSIS
    Initializes secure Grok configuration using Global Security Layer

    .DESCRIPTION
    This function sets up secure API key management using the global security infrastructure.
    Migrates from legacy BusBuddy-specific storage to the global secure vault system.

    .EXAMPLE
    Initialize-SecureGrokConfig

    .NOTES
    Now uses Global-SecureApiManager for enhanced security and cross-project compatibility
    Maintains backward compatibility with legacy BusBuddy configurations
    #>
    [CmdletBinding()]
    [OutputType([System.Boolean])]
    param(
        [Parameter()]
        [SecureString]$ApiKey,

        [Parameter()]
        [string]$LegacyConfigPath = "$env:USERPROFILE\.busbuddy\grok-config.xml"
    )

    Write-Information "Initializing secure Grok configuration using global security layer..." -InformationAction Continue

    try {
        # Step 1: Initialize global secure vault
        if (-not (Initialize-GlobalSecureVault)) {
            throw "Failed to initialize global secure vault"
        }

        # Step 2: Migrate from legacy BusBuddy vault if exists
        $legacyVault = Get-SecretVault -Name "BusBuddySecrets" -ErrorAction SilentlyContinue
        if ($legacyVault) {
            Write-Information "Migrating from legacy BusBuddy vault to global vault..." -InformationAction Continue
            try {
                $legacySecret = Get-Secret -Name "XAI_API_KEY" -Vault "BusBuddySecrets" -ErrorAction SilentlyContinue
                if ($legacySecret) {
                    # Convert SecureString to plain text for migration
                    $bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($legacySecret)
                    $plainKey = [System.Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
                    [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)

                    # Store in global vault
                    Set-GlobalSecureApiKey -ApiKey $plainKey -Provider "XAI"
                    Write-Information "✅ Migrated XAI API key from BusBuddy vault to global vault" -InformationAction Continue

                    # Clean up legacy vault
                    Remove-Secret -Name "XAI_API_KEY" -Vault "BusBuddySecrets" -ErrorAction SilentlyContinue
                    Write-Information "🧹 Cleaned up legacy BusBuddy vault" -InformationAction Continue
                }
            }
            catch {
                Write-Warning "Failed to migrate from legacy BusBuddy vault: $($_.Exception.Message)"
            }
        }

        # Step 3: Migrate from legacy XML storage if exists
        if (Test-Path -Path $LegacyConfigPath) {
            Write-Information "Migrating API key from legacy XML storage to global vault..." -InformationAction Continue
            try {
                $legacyConfig = Import-Clixml -Path $LegacyConfigPath
                $secureKey = $legacyConfig.EncryptedApiKey | ConvertTo-SecureString

                # Convert to plain text for global storage
                $bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureKey)
                $plainKey = [System.Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
                [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)

                Set-GlobalSecureApiKey -ApiKey $plainKey -Provider "XAI"
                Remove-Item -Path $LegacyConfigPath -Force
                Write-Information "✅ Migrated API key from XML to global vault and cleaned up legacy file" -InformationAction Continue
            }
            catch {
                Write-Warning "Failed to migrate from legacy XML: $($_.Exception.Message)"
            }
        }

        # Step 4: Store new API key if provided
        if ($ApiKey) {
            # Convert SecureString to plain text for global storage
            $bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($ApiKey)
            $plainKey = [System.Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
            [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)

            Set-GlobalSecureApiKey -ApiKey $plainKey -Provider "XAI"
            Write-Information "✅ API key stored in global secure vault" -InformationAction Continue
        }

        # Step 5: Verify global vault configuration
        $testResult = Test-GlobalSecureApiConnection -Provider "XAI" -ErrorAction SilentlyContinue
        if ($testResult -and $testResult.Success) {
            Write-Information "✅ Secure Grok configuration initialized successfully with global security" -InformationAction Continue
        }
        else {
            Write-Information "⚠️ Global vault configured but API key may need verification. Use Test-GlobalSecureApiConnection to test." -InformationAction Continue
        }

        return $true
    }
    catch {
        Write-Error "Failed to initialize secure Grok config: $($_.Exception.Message)"
        return $false
    }
}

function Get-SecureApiKey {
    <#
    .SYNOPSIS
    Securely retrieves the XAI API key using Global Security Layer

    .DESCRIPTION
    This function retrieves the API key from the global secure vault with automatic
    migration from legacy storage methods. Uses the global security infrastructure
    for enhanced security and cross-project compatibility.

    .OUTPUTS
    SecureString - The API key as a SecureString object, or $null if an error occurs

    .EXAMPLE
    $apiKey = Get-SecureApiKey
    #>
    [CmdletBinding()]
    [OutputType([SecureString])]
    param()

    try {
        # Use global security layer for retrieval
        $secureApiKey = Get-GlobalSecureApiKey -Provider "XAI"

        if ($secureApiKey -and $secureApiKey.Length -gt 0) {
            Write-Verbose "Retrieved XAI API key from global secure vault"
            return $secureApiKey
        }

        # Fallback: Initialize and migrate if needed
        Write-Information "API key not found in global vault. Attempting migration..." -InformationAction Continue
        if (Initialize-SecureGrokConfig) {
            # Try again after initialization/migration
            $secureApiKey = Get-GlobalSecureApiKey -Provider "XAI"
            if ($secureApiKey -and $secureApiKey.Length -gt 0) {
                return $secureApiKey
            }
        }

        # Final fallback: Check environment for migration
        if ($env:XAI_API_KEY) {
            Write-Warning "API key found in environment variable. Migrating to global vault."
            Set-GlobalSecureApiKey -ApiKey $env:XAI_API_KEY -Provider "XAI" -RemoveFromEnvironment

            # Retrieve the newly stored key
            return Get-GlobalSecureApiKey -Provider "XAI"
        }

        throw "XAI API key not found in global vault or environment variables"
    }
    catch {
        Write-Error "Failed to retrieve secure API key: $($_.Exception.Message)"
        return $null
    }
}

function Set-SecureApiKey {
    <#
    .SYNOPSIS
    Securely stores the XAI API key using Global Security Layer

    .DESCRIPTION
    This function stores the API key in the global secure vault with enhanced security
    and cross-project compatibility. Automatically removes from environment variables.

    .PARAMETER ApiKey
    The API key to store securely

    .PARAMETER RemoveFromEnvironment
    Whether to remove the API key from environment variables after storing securely

    .EXAMPLE
    Set-SecureApiKey -ApiKey "your-api-key" -RemoveFromEnvironment

    .EXAMPLE
    Set-SecureApiKey -ApiKey $env:XAI_API_KEY -RemoveFromEnvironment
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    [OutputType([System.Boolean])]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [string]$ApiKey,

        [Parameter()]
        [switch]$RemoveFromEnvironment = $true
    )

    try {
        # Use global security layer for storage
        $result = Set-GlobalSecureApiKey -ApiKey $ApiKey -Provider "XAI" -RemoveFromEnvironment:$RemoveFromEnvironment

        if ($result) {
            Write-Information "✅ XAI API key stored securely in global vault" -InformationAction Continue
            return $true
        }
        else {
            throw "Failed to store API key in global vault"
        }
    }
    catch {
        Write-Error "Failed to store secure API key: $($_.Exception.Message)"
        return $false
    }
}

function ConvertFrom-SecureApiKey {
    <#
    .SYNOPSIS
    Converts SecureString API key to plain text for API calls

    .DESCRIPTION
    This function safely converts the SecureString API key to plain text
    for use in API calls. Uses the global security layer for enhanced security.

    .PARAMETER SecureApiKey
    The SecureString API key

    .OUTPUTS
    String - The API key in plain text (use immediately and dispose), or $null if an error occurs

    .EXAMPLE
    $secureKey = Get-SecureApiKey
    $plainKey = ConvertFrom-SecureApiKey -SecureApiKey $secureKey
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [SecureString]$SecureApiKey
    )

    try {
        # Use the global security layer for conversion
        return Get-GlobalSecureApiKey -Provider "XAI" -AsPlainText
    }
    catch {
        Write-Error "Failed to convert secure API key: $($_.Exception.Message)"
        return $null
    }
}

function Test-SecureApiKey {
    <#
    .SYNOPSIS
    Tests if the secure API key is properly configured using Global Security Layer

    .DESCRIPTION
    This function validates that the API key is available and properly configured
    in the global secure vault.

    .OUTPUTS
    Boolean - True if API key is properly configured, $false if not or if an error occurs

    .EXAMPLE
    if (Test-SecureApiKey) { "API key is ready" }
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param()

    try {
        # Use global security layer for testing
        $testResult = Test-GlobalSecureApiConnection -Provider "XAI"
        if ($testResult -and $testResult.Success) {
            Write-Information "✅ Secure API key validation successful using global vault" -InformationAction Continue
            return $true
        }
        else {
            Write-Warning "API key test failed or key not properly configured"
            return $false
        }
    }
    catch {
        Write-Error "Secure API key validation failed: $($_.Exception.Message)"
        return $false
    }
}

# Legacy compatibility functions - redirect to global security layer
function Set-AutomatedSecretStoreConfig {
    <#
    .SYNOPSIS
    Legacy compatibility function - redirects to global vault automation

    .DESCRIPTION
    This function provides backward compatibility by redirecting to the global
    vault automation configuration.

    .EXAMPLE
    Set-AutomatedSecretStoreConfig
    #>
    [CmdletBinding()]
    [OutputType([System.Boolean])]
    param()

    Write-Information "Redirecting to global vault automation configuration..." -InformationAction Continue
    return Set-GlobalVaultAutomation
}

function Unlock-AutomatedSecretStore {
    <#
    .SYNOPSIS
    Legacy compatibility function - redirects to global vault unlock

    .DESCRIPTION
    This function provides backward compatibility by redirecting to the global
    vault unlock functionality.

    .EXAMPLE
    Unlock-AutomatedSecretStore
    #>
    [CmdletBinding()]
    [OutputType([System.Boolean])]
    param()

    Write-Verbose "Redirecting to global vault unlock..."
    return Unlock-GlobalSecureVault
}

function Test-GrokApiConnection {
    <#
    .SYNOPSIS
    Tests Grok-4-0709 API connection using Global Security Layer

    .DESCRIPTION
    This function tests the Grok-4-0709 API connection using the global security
    vault for enhanced security and cross-project compatibility.

    .PARAMETER TestMessage
    Optional test message to send to Grok-4-0709. Defaults to a simple test prompt.

    .PARAMETER TimeoutSeconds
    Connection timeout in seconds. Defaults to 30 seconds.

    .OUTPUTS
    PSCustomObject with test results including success status, response, and timing

    .EXAMPLE
    Test-GrokApiConnection

    .EXAMPLE
    Test-GrokApiConnection -TestMessage "Hello Grok, please respond with a brief greeting"
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter()]
        [string]$TestMessage = "Hello Grok-4-0709, please respond with 'BusBuddy API connection successful'",

        [Parameter()]
        [int]$TimeoutSeconds = 30
    )

    Write-Information "Testing Grok-4-0709 API connection using global security layer..." -InformationAction Continue

    try {
        # Use global security layer for API testing
        $result = Test-GlobalSecureApiConnection -Provider "XAI" -TestMessage $TestMessage -TimeoutSeconds $TimeoutSeconds

        if ($result -and $result.Success) {
            Write-Information "✅ Grok-4-0709 API connection successful via global security!" -InformationAction Continue
            Write-Information "   Response time: $([math]::Round($result.ResponseTime, 2)) ms" -InformationAction Continue
            Write-Information "   Grok-4-0709 response: $($result.Response)" -InformationAction Continue
        }

        return $result
    }
    catch {
        Write-Error "❌ Grok-4-0709 API connection failed: $($_.Exception.Message)"

        return [PSCustomObject]@{
            Success = $false
            Provider = "XAI"
            Model = "grok-4-0709"
            ResponseTime = $null
            Response = $null
            TestMessage = $TestMessage
            Timestamp = Get-Date
            Error = $_.Exception.Message
        }
    }
}

# Module initialization complete - all automatic unlock logic triggered on import
try {
    # Step 1: Ensure BusBuddySecrets vault is registered
    $vault = Get-SecretVault -Name "BusBuddySecrets" -ErrorAction SilentlyContinue
    if (-not $vault) {
        Write-Information "Auto-registering BusBuddySecrets vault..." -InformationAction Continue
        Initialize-SecureGrokConfig
    }

    # Step 2: Check if automation is already configured (Microsoft pattern)
    $securePasswordPath = "$env:USERPROFILE\.busbuddy\passwd.xml"

    if (Test-Path $securePasswordPath) {
        # Automation already configured, just unlock (Microsoft pattern)
        Write-Verbose "Automation configured, unlocking vault with stored credentials"
        if (-not (Unlock-AutomatedSecretStore)) {
            Write-Warning "Failed to unlock SecretStore with stored credentials. Manual unlock may be required."
        }
        else {
            Write-Verbose "SecretStore unlocked successfully for automation"

            # Test XAI API key availability for seamless operation
            $secretInfo = Get-SecretInfo -Name "XAI_API_KEY" -Vault "BusBuddySecrets" -ErrorAction SilentlyContinue
            if ($secretInfo) {
                Write-Information "✅ XAI API key available from secure vault for Grok-4 integration" -InformationAction Continue
            } else {
                Write-Information "⚠️ XAI API key not found in vault - use Set-SecureApiKey to store it" -InformationAction Continue
            }
        }
    }
    else {
        # First time setup - automation not yet configured
        Write-Verbose "Automation not configured. Vault may require manual configuration."
        Write-Information "💡 Run Set-AutomatedSecretStoreConfig to enable Microsoft DPAPI automation" -InformationAction Continue
    }

    Write-Information "BusBuddy-SecureConfig module loaded successfully" -InformationAction Continue
}
catch {
    Write-Warning "SecretStore initialization failed: $($_.Exception.Message). Manual configuration may be required."
}

# Export public functions
Export-ModuleMember -Function @(
    'Initialize-SecureGrokConfig',
    'Get-SecureApiKey',
    'Set-SecureApiKey',
    'ConvertFrom-SecureApiKey',
    'Test-SecureApiKey',
    'Test-GrokApiConnection',
    'Set-AutomatedSecretStoreConfig',
    'Unlock-AutomatedSecretStore'
)
