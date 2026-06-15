#requires -Version 7.5
#requires -Modules Microsoft.PowerShell.SecretManagement

<#
.SYNOPSIS
    Global Secure API Key Management Module
    Microsoft SecretManagement Best Practices Implementation

.DESCRIPTION
    This module provides enterprise-grade secure API key management using Microsoft's
    SecretManagement framework. Designed for global use across all PowerShell sessions
    and projects, following Microsoft security best practices.

.NOTES
    Author: BusBuddy Development Team
    Version: 1.0.0 (Global Implementation)
    PowerShell: 7.5.2+
    Dependencies: Microsoft.PowerShell.SecretManagement, Microsoft.PowerShell.SecretStore
    Compliance: Microsoft PowerShell Security Guidelines
#>

# Global constants following Microsoft naming conventions
New-Variable -Name 'GLOBAL_VAULT_NAME' -Value 'GlobalApiSecrets' -Option Constant -Scope Script -Force
New-Variable -Name 'AUTOMATION_CONFIG_PATH' -Value "$env:USERPROFILE\.powershell-security" -Option Constant -Scope Script -Force
New-Variable -Name 'SECURE_PASSWORD_FILE' -Value "$env:USERPROFILE\.powershell-security\vault-passwd.xml" -Option Constant -Scope Script -Force

function Initialize-GlobalSecureVault {
    <#
    .SYNOPSIS
    Initializes the global secure vault for API key management

    .DESCRIPTION
    Creates and configures a global vault following Microsoft's SecretManagement best practices.
    This vault can be used across all PowerShell sessions and projects for secure API storage.

    .PARAMETER Force
    Force re-initialization of the vault

    .EXAMPLE
    Initialize-GlobalSecureVault

    .EXAMPLE
    Initialize-GlobalSecureVault -Force

    .NOTES
    Based on Microsoft SecretManagement documentation:
    https://learn.microsoft.com/en-us/powershell/utility-modules/secretmanagement/
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    [OutputType([System.Boolean])]
    param(
        [Parameter()]
        [switch]$Force
    )

    Write-Information "Initializing Global Secure API Vault..." -InformationAction Continue

    try {
        # Step 1: Check if vault already exists
        $existingVault = Get-SecretVault -Name $GLOBAL_VAULT_NAME -ErrorAction SilentlyContinue

        if ($existingVault -and -not $Force) {
            Write-Information "✅ Global vault '$GLOBAL_VAULT_NAME' already exists" -InformationAction Continue
            return $true
        }

        if ($existingVault -and $Force) {
            if ($PSCmdlet.ShouldProcess($GLOBAL_VAULT_NAME, "Remove existing vault for re-initialization")) {
                Unregister-SecretVault -Name $GLOBAL_VAULT_NAME -ErrorAction SilentlyContinue
                Write-Information "🔄 Removed existing vault for re-initialization" -InformationAction Continue
            }
        }

        # Step 2: Register the global vault
        if ($PSCmdlet.ShouldProcess($GLOBAL_VAULT_NAME, "Register global secure vault")) {
            Register-SecretVault -Name $GLOBAL_VAULT_NAME -ModuleName "Microsoft.PowerShell.SecretStore" -DefaultVault
            Write-Information "✅ Global vault '$GLOBAL_VAULT_NAME' registered successfully" -InformationAction Continue
        }

        # Step 3: Ensure automation directory exists
        if (-not (Test-Path $AUTOMATION_CONFIG_PATH)) {
            New-Item -Path $AUTOMATION_CONFIG_PATH -ItemType Directory -Force | Out-Null
            Write-Information "📁 Created automation config directory" -InformationAction Continue
        }

        # Step 4: Validate vault is accessible
        $vaultInfo = Get-SecretVault -Name $GLOBAL_VAULT_NAME -ErrorAction Stop
        Write-Information "✅ Global secure vault initialized successfully" -InformationAction Continue
        Write-Information "   Vault Name: $($vaultInfo.Name)" -InformationAction Continue
        Write-Information "   Module: $($vaultInfo.ModuleName)" -InformationAction Continue
        Write-Information "   Default: $($vaultInfo.IsDefault)" -InformationAction Continue

        return $true
    }
    catch {
        Write-Error "Failed to initialize global secure vault: $($_.Exception.Message)"
        return $false
    }
}

function Set-GlobalSecureApiKey {
    <#
    .SYNOPSIS
    Stores an API key securely in the global vault

    .DESCRIPTION
    Securely stores API keys using Microsoft's SecretManagement framework with proper
    validation and security measures. Supports multiple API providers with standardized naming.

    .PARAMETER ApiKey
    The API key to store securely

    .PARAMETER Provider
    The API provider (e.g., 'XAI', 'OpenAI', 'Azure', 'Anthropic')

    .PARAMETER KeyName
    Custom key name (defaults to provider-specific naming)

    .PARAMETER RemoveFromEnvironment
    Remove the API key from environment variables after secure storage

    .EXAMPLE
    Set-GlobalSecureApiKey -ApiKey "xai-abc123..." -Provider "XAI"

    .EXAMPLE
    Set-GlobalSecureApiKey -ApiKey $env:OPENAI_API_KEY -Provider "OpenAI" -RemoveFromEnvironment

    .EXAMPLE
    Set-GlobalSecureApiKey -ApiKey "custom-key" -KeyName "CUSTOM_SERVICE_KEY"

    .NOTES
    API keys are validated for basic format requirements based on known provider patterns
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    [OutputType([System.Boolean])]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [ValidateNotNullOrEmpty()]
        [string]$ApiKey,

        [Parameter()]
        [ValidateSet('XAI', 'OpenAI', 'Azure', 'Anthropic', 'Custom')]
        [string]$Provider = 'Custom',

        [Parameter()]
        [string]$KeyName,

        [Parameter()]
        [switch]$RemoveFromEnvironment
    )

    # Initialize vault if needed
    if (-not (Initialize-GlobalSecureVault)) {
        Write-Error "Failed to initialize global secure vault"
        return $false
    }

    # Determine key name based on provider
    if (-not $KeyName) {
        $KeyName = switch ($Provider) {
            'XAI' { 'XAI_API_KEY' }
            'OpenAI' { 'OPENAI_API_KEY' }
            'Azure' { 'AZURE_API_KEY' }
            'Anthropic' { 'ANTHROPIC_API_KEY' }
            default { 'CUSTOM_API_KEY' }
        }
    }

    # Validate API key format
    try {
        if ($ApiKey.Length -lt 10) {
            throw "API key appears too short (minimum 10 characters)"
        }

        # Provider-specific validation
        switch ($Provider) {
            'XAI' {
                if (-not $ApiKey.StartsWith("xai-")) {
                    Write-Warning "API key does not start with 'xai-' - this may not be a valid xAI API key"
                }
            }
            'OpenAI' {
                if (-not $ApiKey.StartsWith("sk-")) {
                    Write-Warning "API key does not start with 'sk-' - this may not be a valid OpenAI API key"
                }
            }
        }

        Write-Verbose "API key format validation passed for $Provider"
    }
    catch {
        Write-Error "API key validation failed: $($_.Exception.Message)"
        return $false
    }

    # Store API key securely
    if ($PSCmdlet.ShouldProcess("$GLOBAL_VAULT_NAME vault", "Store $Provider API key as $KeyName")) {
        try {
            Write-Information "Storing $Provider API key securely..." -InformationAction Continue

            # Ensure vault is unlocked
            $unlockResult = Unlock-GlobalSecureVault
            if (-not $unlockResult) {
                throw "Failed to unlock vault for API key storage"
            }

            Set-Secret -Name $KeyName -Secret $ApiKey -Vault $GLOBAL_VAULT_NAME
            Write-Information "✅ $Provider API key stored securely as '$KeyName'" -InformationAction Continue
        }
        catch {
            Write-Error "Failed to store API key in vault: $($_.Exception.Message)"
            return $false
        }

        # Remove from environment if requested
        if ($RemoveFromEnvironment) {
            $envVariables = @(
                "XAI_API_KEY",
                "OPENAI_API_KEY",
                "AZURE_API_KEY",
                "ANTHROPIC_API_KEY",
                "GROK_API_KEY"
            )

            foreach ($envVar in $envVariables) {
                if (Get-Item -Path "Env:$envVar" -ErrorAction SilentlyContinue) {
                    Remove-Item -Path "Env:$envVar" -ErrorAction SilentlyContinue
                    Write-Information "🧹 Removed $envVar from environment" -InformationAction Continue
                }
            }
        }
    }

    return $true
}

function Get-GlobalSecureApiKey {
    <#
    .SYNOPSIS
    Retrieves an API key securely from the global vault

    .DESCRIPTION
    Securely retrieves API keys from the global vault with automatic unlocking
    and proper error handling. Returns SecureString for maximum security.

    .PARAMETER Provider
    The API provider to retrieve the key for

    .PARAMETER KeyName
    Custom key name to retrieve

    .PARAMETER AsPlainText
    Return the key as plain text (use with caution)

    .EXAMPLE
    $secureKey = Get-GlobalSecureApiKey -Provider "XAI"

    .EXAMPLE
    $plainKey = Get-GlobalSecureApiKey -Provider "OpenAI" -AsPlainText

    .EXAMPLE
    $customKey = Get-GlobalSecureApiKey -KeyName "CUSTOM_SERVICE_KEY"

    .OUTPUTS
    SecureString or String (if AsPlainText specified)
    #>
    [CmdletBinding()]
    [OutputType([SecureString], [string])]
    param(
        [Parameter()]
        [ValidateSet('XAI', 'OpenAI', 'Azure', 'Anthropic', 'Custom')]
        [string]$Provider = 'Custom',

        [Parameter()]
        [string]$KeyName,

        [Parameter()]
        [switch]$AsPlainText
    )

    # Determine key name based on provider
    if (-not $KeyName) {
        $KeyName = switch ($Provider) {
            'XAI' { 'XAI_API_KEY' }
            'OpenAI' { 'OPENAI_API_KEY' }
            'Azure' { 'AZURE_API_KEY' }
            'Anthropic' { 'ANTHROPIC_API_KEY' }
            default { 'CUSTOM_API_KEY' }
        }
    }

    try {
        # Ensure vault is initialized and unlocked
        if (-not (Initialize-GlobalSecureVault)) {
            throw "Failed to initialize global secure vault"
        }

        $unlockResult = Unlock-GlobalSecureVault
        if (-not $unlockResult) {
            throw "Failed to unlock vault for API key retrieval"
        }

        # Check if key exists
        $secretInfo = Get-SecretInfo -Name $KeyName -Vault $GLOBAL_VAULT_NAME -ErrorAction SilentlyContinue
        if (-not $secretInfo) {
            throw "API key '$KeyName' not found in global vault"
        }

        # Retrieve the secure API key
        Write-Verbose "Retrieving $Provider API key from global vault"
        $secureApiKey = Get-Secret -Name $KeyName -Vault $GLOBAL_VAULT_NAME

        if (-not $secureApiKey -or $secureApiKey.Length -eq 0) {
            throw "Retrieved API key is empty or invalid"
        }

        if ($AsPlainText) {
            # Convert to plain text for API usage (use with caution)
            $bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureApiKey)
            $plainKey = [System.Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
            [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
            return $plainKey
        }
        else {
            # Return as SecureString for maximum security
            return $secureApiKey
        }
    }
    catch {
        Write-Error "Failed to retrieve $Provider API key: $($_.Exception.Message)"
        return $null
    }
}

function Set-GlobalVaultAutomation {
    <#
    .SYNOPSIS
    Configures the global vault for automated/unattended access

    .DESCRIPTION
    Configures the SecretStore vault for automation using Microsoft's documented
    DPAPI approach for secure password storage.

    .EXAMPLE
    Set-GlobalVaultAutomation

    .NOTES
    Based on Microsoft SecretManagement automation documentation:
    https://learn.microsoft.com/en-us/powershell/utility-modules/secretmanagement/how-to/using-secrets-in-automation
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    [OutputType([System.Boolean])]
    param()

    try {
        Write-Information "Configuring global vault for automation..." -InformationAction Continue

        # Ensure automation directory exists
        if (-not (Test-Path $AUTOMATION_CONFIG_PATH)) {
            New-Item -Path $AUTOMATION_CONFIG_PATH -ItemType Directory -Force | Out-Null
        }

        # Get credentials for DPAPI storage (Microsoft pattern)
        Write-Information "Setting up DPAPI automation for global vault..." -InformationAction Continue
        $credential = Get-Credential -UserName 'GlobalSecretStore' -Message "Enter password for global vault automation"

        if (-not $credential) {
            throw "Credentials required for automation setup"
        }

        # Store password using DPAPI encryption (Microsoft documented pattern)
        if ($PSCmdlet.ShouldProcess($SECURE_PASSWORD_FILE, "Store encrypted password for automation")) {
            $credential.Password | Export-Clixml -Path $SECURE_PASSWORD_FILE
            Write-Information "✅ Password stored securely using DPAPI encryption" -InformationAction Continue
        }

        # Configure vault for automation
        $password = Import-CliXml -Path $SECURE_PASSWORD_FILE

        # Unlock vault first (required before configuration changes)
        Unlock-SecretStore -Password $password

        # Configure for automation (Microsoft documented settings)
        $storeConfiguration = @{
            PasswordTimeout = 3600  # 1 hour
            Interaction = 'None'    # No user prompts
            Confirm = $false        # No confirmation prompts
        }

        Set-SecretStoreConfiguration @storeConfiguration

        Write-Information "✅ Global vault configured for automation" -InformationAction Continue
        Write-Information "   - Password timeout: 1 hour" -InformationAction Continue
        Write-Information "   - Interaction: None (fully automated)" -InformationAction Continue
        Write-Information "   - Security: DPAPI encryption" -InformationAction Continue

        return $true
    }
    catch {
        Write-Error "Failed to configure global vault automation: $($_.Exception.Message)"
        return $false
    }
}

function Unlock-GlobalSecureVault {
    <#
    .SYNOPSIS
    Unlocks the global secure vault using stored automation credentials

    .DESCRIPTION
    Unlocks the global vault using DPAPI-encrypted password for automation scenarios.

    .EXAMPLE
    Unlock-GlobalSecureVault

    .OUTPUTS
    Boolean indicating success/failure
    #>
    [CmdletBinding()]
    [OutputType([System.Boolean])]
    param()

    try {
        # Check if automation is configured
        if (-not (Test-Path $SECURE_PASSWORD_FILE)) {
            Write-Warning "Global vault automation not configured. Run Set-GlobalVaultAutomation first."
            return $false
        }

        # Microsoft documented pattern: Import password and unlock
        $password = Import-CliXml -Path $SECURE_PASSWORD_FILE
        Unlock-SecretStore -Password $password

        Write-Verbose "Global secure vault unlocked successfully"
        return $true
    }
    catch {
        Write-Error "Failed to unlock global secure vault: $($_.Exception.Message)"
        return $false
    }
}

function Test-GlobalSecureApiConnection {
    <#
    .SYNOPSIS
    Tests API connection using securely stored credentials

    .DESCRIPTION
    Tests API connections for various providers using keys stored in the global vault.

    .PARAMETER Provider
    The API provider to test

    .PARAMETER TestMessage
    Custom test message for the API call

    .PARAMETER TimeoutSeconds
    Connection timeout in seconds

    .EXAMPLE
    Test-GlobalSecureApiConnection -Provider "XAI"

    .EXAMPLE
    Test-GlobalSecureApiConnection -Provider "OpenAI" -TestMessage "Hello from global secure vault"

    .OUTPUTS
    PSCustomObject with test results
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory)]
        [ValidateSet('XAI', 'OpenAI', 'Azure', 'Anthropic')]
        [string]$Provider,

        [Parameter()]
        [string]$TestMessage = "API connection test from global secure vault",

        [Parameter()]
        [int]$TimeoutSeconds = 30
    )

    Write-Information "Testing $Provider API connection using global secure vault..." -InformationAction Continue

    try {
        # Get API key from global vault
        $plainApiKey = Get-GlobalSecureApiKey -Provider $Provider -AsPlainText
        if (-not $plainApiKey) {
            throw "Unable to retrieve $Provider API key from global vault"
        }

        # Provider-specific API testing
        $result = switch ($Provider) {
            'XAI' {
                Test-XAIConnection -ApiKey $plainApiKey -TestMessage $TestMessage -TimeoutSeconds $TimeoutSeconds
            }
            'OpenAI' {
                Test-OpenAIConnection -ApiKey $plainApiKey -TestMessage $TestMessage -TimeoutSeconds $TimeoutSeconds
            }
            default {
                throw "Provider $Provider not yet implemented"
            }
        }

        return $result
    }
    catch {
        Write-Error "Failed to test $Provider API connection: $($_.Exception.Message)"
        return [PSCustomObject]@{
            Success = $false
            Provider = $Provider
            Error = $_.Exception.Message
            Timestamp = Get-Date
        }
    }
    finally {
        # Clear sensitive data from memory
        if ($plainApiKey) {
            Clear-Variable -Name plainApiKey -ErrorAction SilentlyContinue
        }
    }
}

function Test-XAIConnection {
    <#
    .SYNOPSIS
    Tests xAI Grok API connection

    .DESCRIPTION
    Internal function to test xAI Grok-4 API connection with provided credentials.

    .PARAMETER ApiKey
    Plain text API key for testing

    .PARAMETER TestMessage
    Test message to send

    .PARAMETER TimeoutSeconds
    Connection timeout
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory)]
        [string]$ApiKey,

        [Parameter()]
        [string]$TestMessage = "Hello Grok-4, respond with 'Global vault connection successful'",

        [Parameter()]
        [int]$TimeoutSeconds = 30
    )

    try {
        $apiUrl = "https://api.x.ai/v1/chat/completions"
        $headers = @{
            "Authorization" = "Bearer $ApiKey"
            "Content-Type" = "application/json"
        }

        $body = @{
            model = "grok-beta"
            messages = @(
                @{
                    role = "user"
                    content = $TestMessage
                }
            )
            max_tokens = 50
            temperature = 0.1
        } | ConvertTo-Json -Depth 10

        $startTime = Get-Date
        $response = Invoke-RestMethod -Uri $apiUrl -Method Post -Headers $headers -Body $body -TimeoutSec $TimeoutSeconds
        $endTime = Get-Date

        $responseTime = ($endTime - $startTime).TotalMilliseconds
        $assistantMessage = $response.choices[0].message.content

        Write-Information "✅ xAI Grok-4 API connection successful!" -InformationAction Continue
        Write-Information "   Response time: $([math]::Round($responseTime, 2)) ms" -InformationAction Continue

        return [PSCustomObject]@{
            Success = $true
            Provider = "XAI"
            Model = "grok-4-0709"
            ResponseTime = $responseTime
            Response = $assistantMessage
            TestMessage = $TestMessage
            Timestamp = $startTime
            Error = $null
        }
    }
    catch {
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

function Test-OpenAIConnection {
    <#
    .SYNOPSIS
    Tests OpenAI API connection

    .DESCRIPTION
    Internal function to test OpenAI API connection with provided credentials.

    .PARAMETER ApiKey
    Plain text API key for testing

    .PARAMETER TestMessage
    Test message to send

    .PARAMETER TimeoutSeconds
    Connection timeout
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory)]
        [string]$ApiKey,

        [Parameter()]
        [string]$TestMessage = "Hello GPT, respond with 'Global vault connection successful'",

        [Parameter()]
        [int]$TimeoutSeconds = 30
    )

    try {
        $apiUrl = "https://api.openai.com/v1/chat/completions"
        $headers = @{
            "Authorization" = "Bearer $ApiKey"
            "Content-Type" = "application/json"
        }

        $body = @{
            model = "gpt-3.5-turbo"
            messages = @(
                @{
                    role = "user"
                    content = $TestMessage
                }
            )
            max_tokens = 50
            temperature = 0.1
        } | ConvertTo-Json -Depth 10

        $startTime = Get-Date
        $response = Invoke-RestMethod -Uri $apiUrl -Method Post -Headers $headers -Body $body -TimeoutSec $TimeoutSeconds
        $endTime = Get-Date

        $responseTime = ($endTime - $startTime).TotalMilliseconds
        $assistantMessage = $response.choices[0].message.content

        Write-Information "✅ OpenAI API connection successful!" -InformationAction Continue
        Write-Information "   Response time: $([math]::Round($responseTime, 2)) ms" -InformationAction Continue

        return [PSCustomObject]@{
            Success = $true
            Provider = "OpenAI"
            Model = "gpt-3.5-turbo"
            ResponseTime = $responseTime
            Response = $assistantMessage
            TestMessage = $TestMessage
            Timestamp = $startTime
            Error = $null
        }
    }
    catch {
        return [PSCustomObject]@{
            Success = $false
            Provider = "OpenAI"
            Model = "gpt-3.5-turbo"
            ResponseTime = $null
            Response = $null
            TestMessage = $TestMessage
            Timestamp = Get-Date
            Error = $_.Exception.Message
        }
    }
}

# Module initialization - Auto-configure global vault
try {
    # Initialize vault on module load
    if (-not (Initialize-GlobalSecureVault)) {
        Write-Warning "Failed to initialize global secure vault during module load"
    }

    # Attempt auto-unlock if automation is configured
    if (Test-Path $SECURE_PASSWORD_FILE) {
        if (Unlock-GlobalSecureVault) {
            Write-Information "✅ Global secure vault unlocked automatically" -InformationAction Continue
        }
        else {
            Write-Warning "Failed to auto-unlock global vault. Manual unlock may be required."
        }
    }
    else {
        Write-Information "💡 Run Set-GlobalVaultAutomation to enable automatic vault access" -InformationAction Continue
    }

    Write-Information "Global-SecureApiManager module loaded successfully" -InformationAction Continue
}
catch {
    Write-Warning "Global secure vault initialization warning: $($_.Exception.Message)"
}

# Export public functions
Export-ModuleMember -Function @(
    'Initialize-GlobalSecureVault',
    'Set-GlobalSecureApiKey',
    'Get-GlobalSecureApiKey',
    'Set-GlobalVaultAutomation',
    'Unlock-GlobalSecureVault',
    'Test-GlobalSecureApiConnection'
)
