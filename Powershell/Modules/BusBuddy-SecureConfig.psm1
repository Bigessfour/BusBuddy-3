# BusBuddy Secure Configuration Module
# Uses Microsoft SecretManagement for secure API key handling

#requires -Version 7.0
#requires -Modules Microsoft.PowerShell.SecretManagement

[CmdletBinding()]
param()

function Initialize-SecureGrokConfig {
    <#
    .SYNOPSIS
    Initializes secure Grok configuration using Microsoft SecretManagement vault

    .DESCRIPTION
    This function sets up secure API key management following Microsoft's best practices.
    It registers the BusBuddySecrets vault if needed and migrates from legacy storage methods.
    Uses SecretManagement module exclusively to avoid storage fragmentation.

    .EXAMPLE
    Initialize-SecureGrokConfig

    .NOTES
    Requires Microsoft.PowerShell.SecretManagement and Microsoft.PowerShell.SecretStore
    Automatically registers vault and migrates from XML/environment variables
    #>
    [CmdletBinding()]
    [OutputType([System.Boolean])]
    param(
        [Parameter()]
        [SecureString]$ApiKey,

        [Parameter()]
        [string]$LegacyConfigPath = "$env:USERPROFILE\.busbuddy\grok-config.xml"
    )

    Write-Information "Initializing secure Grok configuration..." -InformationAction Continue

    try {
        # Step 1: Ensure BusBuddySecrets vault exists
        $vault = Get-SecretVault -Name "BusBuddySecrets" -ErrorAction SilentlyContinue
        if (-not $vault) {
            Write-Information "Registering BusBuddySecrets vault..." -InformationAction Continue
            Register-SecretVault -Name "BusBuddySecrets" -ModuleName "Microsoft.PowerShell.SecretStore" -DefaultVault
            Write-Information "✅ BusBuddySecrets vault registered successfully" -InformationAction Continue
        }
        else {
            Write-Information "✅ BusBuddySecrets vault already exists" -InformationAction Continue
        }

        # Step 2: Migrate from legacy XML storage if exists
        if (Test-Path -Path $LegacyConfigPath) {
            Write-Information "Migrating API key from legacy XML storage to secure vault..." -InformationAction Continue
            try {
                $legacyConfig = Import-Clixml -Path $LegacyConfigPath
                $secureKey = $legacyConfig.EncryptedApiKey | ConvertTo-SecureString
                Set-Secret -Name "XAI_API_KEY" -Secret $secureKey -Vault "BusBuddySecrets"
                Remove-Item -Path $LegacyConfigPath -Force
                Write-Information "✅ Migrated API key from XML to vault and cleaned up legacy file" -InformationAction Continue
            }
            catch {
                Write-Warning "Failed to migrate from legacy XML: $($_.Exception.Message)"
            }
        }

        # Step 3: Store new API key if provided
        if ($ApiKey) {
            Set-Secret -Name "XAI_API_KEY" -Secret $ApiKey -Vault "BusBuddySecrets"
            Write-Information "✅ API key stored in secure vault" -InformationAction Continue
        }

        # Step 4: Verify vault is properly configured
        $secretInfo = Get-SecretInfo -Name "XAI_API_KEY" -Vault "BusBuddySecrets" -ErrorAction SilentlyContinue
        if ($secretInfo) {
            Write-Information "✅ Secure Grok configuration initialized successfully" -InformationAction Continue
        }
        else {
            Write-Information "⚠️ Vault configured but no API key stored yet. Use Set-SecureApiKey to store your API key." -InformationAction Continue
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
    Securely retrieves the XAI API key using SecretManagement

    .DESCRIPTION
    This function retrieves the API key from the secure vault without exposing it.
    It follows Microsoft's best practices for secret management.

    .OUTPUTS
    SecureString - The API key as a SecureString object, or $null if an error occurs

    .EXAMPLE
    $apiKey = Get-SecureApiKey
    #>
    [CmdletBinding()]
    [OutputType([SecureString])]
    param()

    try {
        # Ensure vault is unlocked using automation
        $unlockResult = Unlock-AutomatedSecretStore
        if (-not $unlockResult) {
            Write-Warning "SecretStore is not unlocked. Use Set-AutomatedSecretStoreConfig to configure automation."
        }

        # Initialize secure configuration if needed
        if (-not (Initialize-SecureGrokConfig)) {
            throw [System.Exception]::new("Failed to initialize secure configuration")
        }

        # Attempt to get API key from secure vault
        $secretInfo = Get-SecretInfo -Name "XAI_API_KEY" -Vault "BusBuddySecrets" -ErrorAction SilentlyContinue

        if ($secretInfo) {
            Write-Verbose "Retrieving API key from secure vault"
            $secureApiKey = Get-Secret -Name "XAI_API_KEY" -Vault "BusBuddySecrets"

            if ($secureApiKey -is [SecureString] -and $secureApiKey.Length -gt 0) {
                # Return SecureString directly for secure handling
                # Securely retrieve the API key as SecureString from the vault
                return $secureApiKey
            }
        }

        # Fallback: Check if key exists in environment (for migration)
        if ($env:XAI_API_KEY) {
            Write-Warning "API key found in environment variable. Migrating to secure vault."
            # Convert environment variable to SecureString for secure storage
            $envApiKey = ConvertTo-SecureString -String $env:XAI_API_KEY -AsPlainText -Force
            Set-Secret -Name "XAI_API_KEY" -Secret $envApiKey -Vault "BusBuddySecrets"

            # Remove from environment for security
            Remove-Item -Path "Env:XAI_API_KEY" -ErrorAction SilentlyContinue
            Write-Information "✅ API key migrated from environment to secure vault and removed from environment" -InformationAction Continue

            # Retrieve the newly stored key
            $secureApiKey = Get-Secret -Name "XAI_API_KEY" -Vault "BusBuddySecrets"
            return $secureApiKey
        }

        throw [System.Exception]::new("XAI API key not found in secure vault or environment variables")
    }
    catch {
        Write-Error "Failed to retrieve secure API key: $($_.Exception.Message)"
        return $null
    }
}

function Set-SecureApiKey {
    <#
    .SYNOPSIS
    Securely stores the XAI API key using SecretManagement

    .DESCRIPTION
    This function stores the API key in the secure vault and removes it from
    environment variables for better security.

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
        [switch]$RemoveFromEnvironment
    )

    # Initialize secure configuration if needed
    try {
        if (-not (Initialize-SecureGrokConfig)) {
            throw "Failed to initialize secure configuration"
        }
    }
    catch {
        Write-Error "Failed to initialize secure configuration: $($_.Exception.Message)"
        return $false
    }

    # Validate API key format (xAI keys start with 'xai-' and have variable length)
    try {
        if ([string]::IsNullOrEmpty($ApiKey)) {
            throw [System.Exception]::new("API key cannot be null or empty")
        }
        if ($ApiKey.Length -lt 20) {
            throw [System.Exception]::new("API key appears too short (minimum 20 characters)")
        }
        if (-not $ApiKey.StartsWith("xai-")) {
            Write-Warning "API key does not start with 'xai-' - this may not be a valid xAI API key"
        }
        # xAI keys have variable lengths, so we won't enforce a specific length
        Write-Verbose "API key format validation passed for xAI"
    }
    catch {
        Write-Error "API key validation failed: $($_.Exception.Message)"
        return $false
    }

    # Store API key securely
    if ($PSCmdlet.ShouldProcess("BusBuddySecrets vault", "Store API key securely")) {
        try {
            Write-Information "Storing API key securely in vault..." -InformationAction Continue
            Set-Secret -Name "XAI_API_KEY" -Secret $ApiKey -Vault "BusBuddySecrets"
            Write-Information "✅ API key stored securely in BusBuddySecrets vault" -InformationAction Continue
        }
        catch {
            Write-Error "Failed to store API key in vault: $($_.Exception.Message)"
            return $false
        }

        # Remove from environment if requested
        if ($RemoveFromEnvironment) {
            try {
                if ($env:XAI_API_KEY) {
                    Remove-Item -Path "Env:XAI_API_KEY" -ErrorAction SilentlyContinue
                    Write-Information "✅ Removed API key from environment variables" -InformationAction Continue
                }
                if ($env:GROK_API_KEY) {
                    Remove-Item -Path "Env:GROK_API_KEY" -ErrorAction SilentlyContinue
                    Write-Information "✅ Removed legacy API key from environment variables" -InformationAction Continue
                }
            }
            catch {
                Write-Error "Failed to remove API key from environment variables: $($_.Exception.Message)"
                # Continue, as this is not critical for secure storage
            }
        }
    }

    return $true
}

function ConvertFrom-SecureApiKey {
    <#
    .SYNOPSIS
    Converts SecureString API key to plain text for API calls

    .DESCRIPTION
    This function safely converts the SecureString API key to plain text
    for use in API calls. The plain text is only in memory temporarily.

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
        # Convert SecureString to plain text for API usage
        # cspell:ignore bstr BSTR
        $bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureApiKey)
        $plainKey = [System.Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)

        # Clear the BSTR from memory for security
        [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)

        return $plainKey
    }
    catch {
        Write-Error "Failed to convert secure API key: $($_.Exception.Message)"
        return $null
    }
}

function Test-SecureApiKey {
    <#
    .SYNOPSIS
    Tests if the secure API key is properly configured

    .DESCRIPTION
    This function validates that the API key is available and properly configured
    in the secure vault.

    .OUTPUTS
    Boolean - True if API key is properly configured, $false if not or if an error occurs

    .EXAMPLE
    if (Test-SecureApiKey) { "API key is ready" }
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param()

    try {
        $secureKey = Get-SecureApiKey
        if ($null -eq $secureKey) {
            return $false
        }

        $plainKey = ConvertFrom-SecureApiKey -SecureApiKey $secureKey
        if ([string]::IsNullOrEmpty($plainKey) -or $plainKey.Length -lt 20) {
            return $false
        }

        Write-Information "✅ Secure API key validation successful" -InformationAction Continue
        return $true
    }
    catch {
        Write-Error "Secure API key validation failed: $($_.Exception.Message)"
        return $false
    }
}

function Set-AutomatedSecretStoreConfig {
    <#
    .SYNOPSIS
    Configures SecretStore for automated/unattended access following Microsoft best practices

    .DESCRIPTION
    This function configures the SecretStore vault for automation scenarios using Microsoft's
    exact documented approach from official documentation. Uses DPAPI for secure password storage
    and configures the vault for non-interactive automation.

    IMPORTANT: Since vault is already unlocked with manual password, this will capture
    that password and store it securely using Microsoft's DPAPI approach for future automation.

    .EXAMPLE
    Set-AutomatedSecretStoreConfig

    .NOTES
    Based on Microsoft documentation:
    https://learn.microsoft.com/en-us/powershell/utility-modules/secretmanagement/how-to/using-secrets-in-automation
    Exact implementation following Microsoft's documented patterns.
    #>
    [CmdletBinding()]
    [OutputType([System.Boolean])]
    param()

    try {
        Write-Information "Configuring SecretStore for automation following Microsoft documentation..." -InformationAction Continue

        # Microsoft documented path pattern for secure password storage
        $securePasswordPath = "$env:USERPROFILE\.busbuddy\passwd.xml"

        # Ensure directory exists (Microsoft pattern)
        $passwordDir = Split-Path $securePasswordPath -Parent
        if (-not (Test-Path $passwordDir)) {
            New-Item -Path $passwordDir -ItemType Directory -Force | Out-Null
        }

        # Microsoft documented pattern: Get-Credential with unimportant UserName
        # Since vault is unlocked, we need to capture the password for DPAPI storage
        Write-Information "Since vault is already unlocked, please re-enter the same password for DPAPI automation setup..." -InformationAction Continue
        $credential = Get-Credential -UserName 'SecureStore' -Message "Re-enter your SecretStore password for automation (same password you just used)"

        # Microsoft documented: Export password as SecureString encrypted by DPAPI
        $credential.Password | Export-Clixml -Path $securePasswordPath
        Write-Information "✅ Password stored securely using Windows Data Protection (DPAPI)" -InformationAction Continue

        # Microsoft documented pattern: Register vault (ensure it exists)
        try {
            $existingVault = Get-SecretVault -Name SecretStore -ErrorAction SilentlyContinue
            if (-not $existingVault) {
                Register-SecretVault -Name SecretStore -ModuleName Microsoft.PowerShell.SecretStore -DefaultVault
                Write-Information "✅ SecretStore vault registered" -InformationAction Continue
            }
            else {
                Write-Information "✅ SecretStore vault already registered" -InformationAction Continue
            }
        }
        catch {
            Write-Error "Failed to register SecretStore vault: $($_.Exception.Message)"
            return $false
        }

        # Microsoft documented exact pattern: First unlock vault, then configure for automation
        $password = Import-CliXml -Path $securePasswordPath

        # Step 1: Unlock the vault first (required before configuration changes)
        Write-Information "Unlocking vault for configuration changes..." -InformationAction Continue
        Unlock-SecretStore -Password $password

        # Step 2: Check current configuration
        $currentConfig = Get-SecretStoreConfiguration
        Write-Information "Current SecretStore configuration:" -InformationAction Continue
        Write-Information "   - Authentication: $($currentConfig.Authentication)" -InformationAction Continue
        Write-Information "   - PasswordTimeout: $($currentConfig.PasswordTimeout)" -InformationAction Continue
        Write-Information "   - Interaction: $($currentConfig.Interaction)" -InformationAction Continue

        # Step 3: Microsoft documented approach for existing vaults
        # From Microsoft docs: "You can change the configuration of a vault using the Set-SecretStoreConfiguration cmdlet"
        # Key insight: Vault must be unlocked BEFORE changing configuration
        $storeConfiguration = @{
            PasswordTimeout = 3600 # 1 hour (Microsoft documented value)
            Interaction = 'None'   # Microsoft: "so that SecretStore never prompts the user"
            Confirm = $false       # Microsoft: "so that PowerShell does not prompt for confirmation"
        }

        # Apply automation configuration (vault is now unlocked, so this should work)
        Set-SecretStoreConfiguration @storeConfiguration

        Write-Information "✅ SecretStore configured for automation using Microsoft's exact pattern:" -InformationAction Continue
        Write-Information "   - Authentication: Password" -InformationAction Continue
        Write-Information "   - PasswordTimeout: 3600 seconds (1 hour)" -InformationAction Continue
        Write-Information "   - Interaction: None (no user prompts)" -InformationAction Continue
        Write-Information "   - Password: Stored via DPAPI encryption" -InformationAction Continue

        return $true
    }
    catch {
        Write-Error "Failed to configure automated SecretStore access: $($_.Exception.Message)"
        return $false
    }
}

function Unlock-AutomatedSecretStore {
    <#
    .SYNOPSIS
    Unlocks SecretStore using stored password for automation scenarios

    .DESCRIPTION
    Unlocks the SecretStore vault using the password stored via DPAPI encryption.
    Follows Microsoft's exact documented pattern for automation scenarios.

    .EXAMPLE
    Unlock-AutomatedSecretStore

    .NOTES
    Based on Microsoft documentation automation pattern:
    https://learn.microsoft.com/en-us/powershell/utility-modules/secretmanagement/how-to/using-secrets-in-automation
    Exact implementation: $password = Import-CliXml -Path $securePasswordPath; Unlock-SecretStore -Password $password
    #>
    [CmdletBinding()]
    [OutputType([System.Boolean])]
    param()

    try {
        $securePasswordPath = "$env:USERPROFILE\.busbuddy\passwd.xml"

        if (-not (Test-Path $securePasswordPath)) {
            Write-Warning "Secure password file not found. Run Set-AutomatedSecretStoreConfig first."
            return $false
        }

        # Microsoft documented exact pattern: Import password and unlock
        $password = Import-CliXml -Path $securePasswordPath
        Unlock-SecretStore -Password $password

        Write-Information "✅ SecretStore unlocked for current session using Microsoft automation pattern" -InformationAction Continue
        return $true
    }
    catch {
        Write-Error "Failed to unlock SecretStore: $($_.Exception.Message)"
        return $false
    }
}

function Test-GrokApiConnection {
    <#
    .SYNOPSIS
    Tests Grok-4-0709 API connection using secure vault credentials

    .DESCRIPTION
    This function tests the Grok-4-0709 API connection automatically using credentials
    from the secure vault. It handles automatic vault unlocking and provides
    comprehensive connection testing.

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
        [string]$TestMessage = "Hello Grok-4-0709, please respond with 'API connection successful'",

        [Parameter()]
        [int]$TimeoutSeconds = 30
    )

    Write-Information "Testing Grok-4-0709 API connection with secure vault integration..." -InformationAction Continue

    try {
        # Get API key using secure configuration with automatic unlock
        $secureApiKey = Get-SecureApiKey
        if (-not $secureApiKey) {
            throw [System.Exception]::new("Unable to retrieve XAI API key from secure vault")
        }

        # Convert to plain text for API call (done securely)
        $plainApiKey = ConvertFrom-SecureApiKey -SecureApiKey $secureApiKey
        if (-not $plainApiKey) {
            throw [System.Exception]::new("Unable to convert secure API key for API call")
        }

        # Prepare API request
        $apiUrl = "https://api.x.ai/v1/chat/completions"
        $headers = @{
            "Authorization" = "Bearer $plainApiKey"
            "Content-Type" = "application/json"
        }

        $body = @{
            model = "grok-4-0709"
            messages = @(
                @{
                    role = "user"
                    content = $TestMessage
                }
            )
            max_tokens = 50
            temperature = 0.1
        } | ConvertTo-Json -Depth 10

        Write-Information "Sending test request to Grok-4-0709..." -InformationAction Continue
        $startTime = Get-Date

        # Make API request with timeout
        $response = Invoke-RestMethod -Uri $apiUrl -Method Post -Headers $headers -Body $body -TimeoutSec $TimeoutSeconds
        $endTime = Get-Date
        $responseTime = ($endTime - $startTime).TotalMilliseconds

        # Process successful response
        $assistantMessage = $response.choices[0].message.content
        Write-Information "✅ Grok-4-0709 API connection successful!" -InformationAction Continue
        Write-Information "   Response time: $([math]::Round($responseTime, 2)) ms" -InformationAction Continue
        Write-Information "   Grok-4-0709 response: $assistantMessage" -InformationAction Continue

        return [PSCustomObject]@{
            Success = $true
            ResponseTime = $responseTime
            ApiResponse = $assistantMessage
            TestMessage = $TestMessage
            Timestamp = $startTime
            Model = "grok-4-0709"
            Error = $null
        }

    } catch {
        $errorMessage = $_.Exception.Message
        Write-Error "❌ Grok-4-0709 API connection failed: $errorMessage"

        return [PSCustomObject]@{
            Success = $false
            ResponseTime = $null
            ApiResponse = $null
            TestMessage = $TestMessage
            Timestamp = Get-Date
            Model = "grok-4-0709"
            Error = $errorMessage
        }
    } finally {
        # Clear sensitive data from memory
        if ($plainApiKey) {
            Clear-Variable -Name plainApiKey -ErrorAction SilentlyContinue
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
