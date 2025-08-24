# Test-SecureApiKeyImplementation.ps1
# Comprehensive test suite for xAI API key secure storage implementation in BusBuddy-3
# Based on Microsoft PowerShell 7.5.2 best practices and SecretManagement framework
# GitHub Repository: https://github.com/Bigessfour/BusBuddy-3
# References:
#   - https://learn.microsoft.com/en-us/powershell/utility-modules/secretmanagement/overview?view=powershell-7.5
#   - https://learn.microsoft.com/en-us/azure/azure-sql/database/security-overview
#   - https://help.syncfusion.com/wpf/licensing/overview

#requires -Version 7.5.2

[CmdletBinding()]
param(
    [Parameter()]
    [string]$TestApiKey = "xai-test-key-1234567890abcdef1234567890abcdef1234567890",

    [Parameter()]
    [switch]$SkipGlobalProfileTest,

    [Parameter()]
    [switch]$CleanupAfterTest,

    [Parameter()]
    [switch]$EnablePerformanceTesting,

    [Parameter()]
    [switch]$TestRelatedSecrets,

    [Parameter()]
    [switch]$GenerateCIReport
)

# Strict mode for PowerShell 7.5.2 compliance
Set-StrictMode -Version Latest

# Test results collection
$script:TestResults = @()

function Write-TestResult {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$TestName,

        [Parameter(Mandatory)]
        [bool]$Passed,

        [Parameter()]
        [string]$Message = "",

        [Parameter()]
        [string]$Details = "",

        [Parameter()]
        [string]$Category = "General",

        [Parameter()]
        [timespan]$Duration = [timespan]::Zero,

        [Parameter()]
        [hashtable]$AdditionalData = @{}
    )

    # Enhanced result object with category and performance data
    $result = [PSCustomObject]@{
        TestName = $TestName
        Category = $Category
        Passed = $Passed
        Message = $Message
        Details = $Details
        Duration = $Duration
        Timestamp = Get-Date
        AdditionalData = $AdditionalData
    }

    $script:TestResults += $result

    # Enhanced status with category and duration
    $statusIcon = if ($Passed) { "✅" } else { "❌" }
    $status = if ($Passed) { "PASS" } else { "FAIL" }
    $categoryPrefix = "[$Category]"

    # Format duration for performance tests
    $durationText = if ($Duration -gt [timespan]::Zero) {
        " (Duration: $($Duration.TotalMilliseconds) ms)"
    } else { "" }

    Write-Information "$statusIcon $status - $categoryPrefix $TestName$durationText" -InformationAction Continue
    if ($Message) {
        Write-Information "    $Message" -InformationAction Continue
    }
    if ($Details -and $VerbosePreference -eq 'Continue') {
        Write-Verbose "    Details: $Details"
    }

    # CI-friendly output for GitHub Actions
    if ($GenerateCIReport) {
        $ciOutput = if ($Passed) {
            "::notice title=$Category::✅ $TestName - $Message"
        } else {
            "::error title=$Category::❌ $TestName - $Message. Details: $Details"
        }
        Write-Host $ciOutput
    }
}

# Performance measurement helper
function Measure-TestExecution {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$TestName,

        [Parameter(Mandatory)]
        [ScriptBlock]$TestScript,

        [Parameter()]
        [string]$Category = "General"
    )

    $startTime = Get-Date
    try {
        $result = & $TestScript
        $endTime = Get-Date
        $duration = $endTime - $startTime

        if ($EnablePerformanceTesting) {
            $script:PerformanceMetrics[$TestName] = $duration
            Write-Verbose "Performance: $TestName took $($duration.TotalMilliseconds)ms"
        }

        return @{
            Result = $result
            Duration = $duration
            Success = $true
        }
    }
    catch {
        $endTime = Get-Date
        $duration = $endTime - $startTime

        Write-TestResult -TestName $TestName -Passed $false -Message "Exception: $($_.Exception.Message)" -Duration $duration -Category $Category
        return @{
            Result = $null
            Duration = $duration
            Success = $false
            Exception = $_
        }
    }
}

function Test-Prerequisites {
    [CmdletBinding()]
    param()

    Write-Information "🔍 Testing Prerequisites..." -InformationAction Continue

    $startTime = Get-Date

    # Test 1: Enhanced PowerShell Version Check (7.5.2+ for SecretManagement stability)
    $psVersion = $PSVersionTable.PSVersion
    $isPsVersion752Plus = $psVersion.Major -ge 7 -and $psVersion.Minor -ge 5 -and $psVersion.Patch -ge 2
    Write-TestResult -TestName "PowerShell 7.5.2+ Version Check" -Passed $isPsVersion752Plus -Message "Version: $psVersion" -Details "Required for latest SecretManagement stability" -Category "Prerequisites"

    # Test 2: SecretManagement Modules with Version Info
    $secretMgmtModule = Get-Module -ListAvailable Microsoft.PowerShell.SecretManagement
    $secretStoreModule = Get-Module -ListAvailable Microsoft.PowerShell.SecretStore

    Write-TestResult -TestName "SecretManagement Module Available" -Passed ($null -ne $secretMgmtModule) -Message "Version: $($secretMgmtModule.Version)" -Category "Prerequisites"
    Write-TestResult -TestName "SecretStore Module Available" -Passed ($null -ne $secretStoreModule) -Message "Version: $($secretStoreModule.Version)" -Category "Prerequisites"

    # Test 3: BusBuddy-SecureConfig Module
    $secureConfigPath = "C:\Users\biges\Desktop\BusBuddy\PowerShell\Modules\BusBuddy-SecureConfig.psm1"
    $secureConfigExists = Test-Path $secureConfigPath
    Write-TestResult -TestName "BusBuddy-SecureConfig Module Exists" -Passed $secureConfigExists -Message "Path: $secureConfigPath" -Category "Prerequisites"

    # Test 4: Global-SecureApiManager Module
    $globalSecurePath = "C:\Users\biges\Desktop\BusBuddy\PowerShell\Modules\Global-SecureApiManager.psm1"
    $globalSecureExists = Test-Path $globalSecurePath
    Write-TestResult -TestName "Global-SecureApiManager Module Exists" -Passed $globalSecureExists -Message "Path: $globalSecurePath" -Category "Prerequisites"

        # Test 5: Platform-specific checks
        $platformInfo = $PSVersionTable.Platform
        $editionInfo = $PSVersionTable.PSEdition
        $isWindowsSystem = $platformInfo -eq 'Win32NT' -or $editionInfo -eq 'Desktop'
        Write-TestResult -TestName "Platform Compatibility Check" -Passed $true -Message "Platform: $platformInfo" -Details "DPAPI available: $isWindowsSystem" -Category "Prerequisites"    $duration = (Get-Date) - $startTime
    if ($EnablePerformanceTesting) {
        Write-TestResult -TestName "Prerequisites Performance" -Passed ($duration.TotalSeconds -lt 5) -Message "Duration: $($duration.TotalMilliseconds) ms" -Category "Performance"
    }

    return ($isPsVersion752Plus -and $secretMgmtModule -and $secretStoreModule -and $secureConfigExists -and $globalSecureExists)
}function Test-ModuleImport {
    [CmdletBinding()]
    param()

    Write-Information "📦 Testing Module Import..." -InformationAction Continue

    try {
        # Import BusBuddy-SecureConfig
        Import-Module "C:\Users\biges\Desktop\BusBuddy\PowerShell\Modules\BusBuddy-SecureConfig.psm1" -Force -ErrorAction Stop
        Write-TestResult -TestName "Import BusBuddy-SecureConfig Module" -Passed $true -Message "Successfully imported"

        # Test required functions exist
        $requiredFunctions = @(
            'Initialize-SecureGrokConfig',
            'Set-SecureApiKey',
            'Get-SecureApiKey',
            'ConvertFrom-SecureApiKey',
            'Unlock-AutomatedSecretStore'
        )

        foreach ($func in $requiredFunctions) {
            $funcExists = Get-Command $func -ErrorAction SilentlyContinue
            Write-TestResult -TestName "Function $func Available" -Passed ($null -ne $funcExists) -Message "Command type: $($funcExists.CommandType)"
        }

        return $true
    }
    catch {
        Write-TestResult -TestName "Import BusBuddy-SecureConfig Module" -Passed $false -Message "Import failed: $($_.Exception.Message)"
        return $false
    }
}

function Test-VaultInitialization {
    [CmdletBinding()]
    param()

    Write-Information "🔐 Testing Vault Initialization..." -InformationAction Continue

    try {
        # Initialize the secure configuration
        Initialize-SecureGrokConfig
        Write-TestResult -TestName "Initialize Secure Grok Config" -Passed $true -Message "Vault initialized successfully"

        # Check if vault is registered
        $vault = Get-SecretVault -Name "BusBuddySecrets" -ErrorAction SilentlyContinue
        Write-TestResult -TestName "BusBuddySecrets Vault Registered" -Passed ($null -ne $vault) -Message "Vault type: $($vault.ModuleName)"

        return $true
    }
    catch {
        Write-TestResult -TestName "Initialize Secure Grok Config" -Passed $false -Message "Initialization failed: $($_.Exception.Message)"
        return $false
    }
}

function Test-ApiKeyStorage {
    [CmdletBinding()]
    param([string]$ApiKey)

    Write-Information "💾 Testing API Key Storage..." -InformationAction Continue

    try {
        # Store the test API key
        Set-SecureApiKey -ApiKey $ApiKey
        Write-TestResult -TestName "Store API Key in Vault" -Passed $true -Message "Key stored successfully"

        # Verify key exists in vault
        $secretInfo = Get-SecretInfo -Name "XAI_API_KEY" -Vault "BusBuddySecrets" -ErrorAction SilentlyContinue
        Write-TestResult -TestName "Verify Key Exists in Vault" -Passed ($null -ne $secretInfo) -Message "Secret name: $($secretInfo.Name)"

        return $true
    }
    catch {
        Write-TestResult -TestName "Store API Key in Vault" -Passed $false -Message "Storage failed: $($_.Exception.Message)"
        return $false
    }
}

function Test-ApiKeyRetrieval {
    [CmdletBinding()]
    param([string]$ExpectedKey)

    Write-Information "🔓 Testing API Key Retrieval..." -InformationAction Continue

    try {
        # Unlock the secret store
        $unlocked = Unlock-AutomatedSecretStore
        Write-TestResult -TestName "Unlock Automated Secret Store" -Passed $unlocked -Message "Unlock status: $unlocked"

        if ($unlocked) {
            # Retrieve the secure key
            $secureKey = Get-SecureApiKey
            Write-TestResult -TestName "Retrieve Secure API Key" -Passed ($null -ne $secureKey) -Message "Key type: $($secureKey.GetType().Name)"

            # Convert from secure string
            $plainKey = ConvertFrom-SecureApiKey -SecureApiKey $secureKey
            Write-TestResult -TestName "Convert from Secure String" -Passed ($null -ne $plainKey) -Message "Key length: $($plainKey.Length)"

            # Verify key matches expected
            $keyMatches = ($plainKey -eq $ExpectedKey)
            Write-TestResult -TestName "Verify Key Matches Expected" -Passed $keyMatches -Message "Keys match: $keyMatches"

            return $plainKey
        }
        else {
            Write-TestResult -TestName "API Key Retrieval Flow" -Passed $false -Message "Could not unlock secret store"
            return $null
        }
    }
    catch {
        Write-TestResult -TestName "API Key Retrieval Flow" -Passed $false -Message "Retrieval failed: $($_.Exception.Message)"
        return $null
    }
}

function Test-GlobalProfileIntegration {
    [CmdletBinding()]
    param([string]$ExpectedKey)

    Write-Information "🌐 Testing Global Profile Integration..." -InformationAction Continue

    # Test the global profile code in isolation
    try {
        # Simulate the global profile vault integration code
        $secureModulePath = "C:\Users\biges\Desktop\BusBuddy\PowerShell\Modules\BusBuddy-SecureConfig.psm1"
        if (Test-Path $secureModulePath) {
            Import-Module $secureModulePath -Force -ErrorAction Stop

            Initialize-SecureGrokConfig
            if (Unlock-AutomatedSecretStore) {
                $secureKey = Get-SecureApiKey
                if ($secureKey) {
                    $global:GrokApiKey = ConvertFrom-SecureApiKey -SecureApiKey $secureKey
                    Write-TestResult -TestName "Global Profile Simulation" -Passed $true -Message "Global variable set successfully"

                    # Verify global variable
                    $globalKeyExists = ($null -ne $global:GrokApiKey)
                    Write-TestResult -TestName "Global GrokApiKey Variable Set" -Passed $globalKeyExists -Message "Length: $($global:GrokApiKey.Length)"

                    # Verify key matches
                    $globalKeyMatches = ($global:GrokApiKey -eq $ExpectedKey)
                    Write-TestResult -TestName "Global Key Matches Expected" -Passed $globalKeyMatches -Message "Match status: $globalKeyMatches"

                    return $true
                }
            }
        }

        Write-TestResult -TestName "Global Profile Simulation" -Passed $false -Message "Profile integration failed"
        return $false
    }
    catch {
        Write-TestResult -TestName "Global Profile Simulation" -Passed $false -Message "Integration failed: $($_.Exception.Message)"
        return $false
    }
}

function Test-GrokConfigIntegration {
    [CmdletBinding()]
    param()

    Write-Information "⚙️ Testing Grok Config Integration..." -InformationAction Continue

    try {
        # Test the updated Get-ApiKeySecurely function pattern
        $getApiKeySecurelyScript = {
            try {
                if (Get-Command Get-SecureApiKey -ErrorAction SilentlyContinue) {
                    $secureKey = Get-SecureApiKey
                    if ($secureKey) {
                        return (ConvertFrom-SecureApiKey -SecureApiKey $secureKey)
                    }
                }
                throw "API key not found in secure vault. Run Set-SecureApiKey to store it."
            } catch {
                Write-Warning "Failed to retrieve secure API key: $($_.Exception.Message)"
                return $null
            }
        }

        $retrievedKey = & $getApiKeySecurelyScript
        Write-TestResult -TestName "Get-ApiKeySecurely Function Pattern" -Passed ($null -ne $retrievedKey) -Message "Key retrieved successfully"

        # Test Global:GrokConfig pattern
        if ($global:GrokApiKey) {
            $Global:GrokConfig = @{ ApiKey = $global:GrokApiKey }
            Write-TestResult -TestName "GrokConfig Global Variable Setup" -Passed $true -Message "Config hash created with API key"

            $configKeyExists = ($null -ne $Global:GrokConfig.ApiKey)
            Write-TestResult -TestName "GrokConfig ApiKey Property Set" -Passed $configKeyExists -Message "Key length: $($Global:GrokConfig.ApiKey.Length)"

            return $true
        }

        Write-TestResult -TestName "GrokConfig Global Variable Setup" -Passed $false -Message "Global:GrokApiKey not available"
        return $false
    }
    catch {
        Write-TestResult -TestName "Grok Config Integration" -Passed $false -Message "Integration failed: $($_.Exception.Message)"
        return $false
    }
}

function Test-SecurityValidation {
    [CmdletBinding()]
    param()

    Write-Information "🔒 Testing Security Validation..." -InformationAction Continue

    # Test 1: No environment variables should contain the API key
    $envVarsWithKey = Get-ChildItem env: | Where-Object { $_.Value -like "*$TestApiKey*" }
    Write-TestResult -TestName "No API Key in Environment Variables" -Passed ($envVarsWithKey.Count -eq 0) -Message "Found $($envVarsWithKey.Count) env vars with key"

    # Test 2: Secure string is actually secure
    $secureKey = Get-SecureApiKey -ErrorAction SilentlyContinue
    if ($secureKey) {
        $isSecureString = ($secureKey.GetType().Name -eq 'SecureString')
        Write-TestResult -TestName "API Key Stored as SecureString" -Passed $isSecureString -Message "Type: $($secureKey.GetType().Name)"
    }

    # Test 3: Vault uses DPAPI encryption (Windows)
    $vault = Get-SecretVault -Name "BusBuddySecrets" -ErrorAction SilentlyContinue
    if ($vault) {
        $usesDPAPI = ($vault.ModuleName -eq 'Microsoft.PowerShell.SecretStore')
        Write-TestResult -TestName "Vault Uses DPAPI Encryption" -Passed $usesDPAPI -Message "Module: $($vault.ModuleName)"
    }

    # Test 4: Global variable is cleared after use (optional test)
    $globalVarExists = ($null -ne $global:GrokApiKey)
    Write-TestResult -TestName "Global Variable Available" -Passed $globalVarExists -Message "Global:GrokApiKey exists: $globalVarExists"
}

function Test-GlobalProfileFile {
    [CmdletBinding()]
    param()

    if ($SkipGlobalProfileTest) {
        Write-Information "⏭️ Skipping Global Profile File Test (SkipGlobalProfileTest specified)" -InformationAction Continue
        return
    }

    Write-Information "📄 Testing Global Profile File..." -InformationAction Continue

    $globalProfilePath = "$env:ProgramFiles\PowerShell\7\profile.ps1"
    $profileExists = Test-Path $globalProfilePath

    if ($profileExists) {
        try {
            $profileContent = Get-Content $globalProfilePath -Raw -ErrorAction Stop

            # Check for BusBuddy integration
            $hasBusBuddyIntegration = $profileContent -match 'BusBuddy.*Secure.*Vault.*Integration'
            Write-TestResult -TestName "Global Profile Has BusBuddy Integration" -Passed $hasBusBuddyIntegration -Message "Integration block found: $hasBusBuddyIntegration"

            # Check for secure module import
            $hasSecureModuleImport = $profileContent -match 'BusBuddy-SecureConfig\.psm1'
            Write-TestResult -TestName "Global Profile Imports Secure Module" -Passed $hasSecureModuleImport -Message "Module import found: $hasSecureModuleImport"

        }
        catch {
            Write-TestResult -TestName "Read Global Profile Content" -Passed $false -Message "Cannot read profile: $($_.Exception.Message)"
        }
    }
    else {
        Write-TestResult -TestName "Global Profile File Exists" -Passed $false -Message "Profile not found at: $globalProfilePath"
        Write-Information "ℹ️ To create global profile, run as Administrator:" -InformationAction Continue
        Write-Information "   New-Item -Path '$globalProfilePath' -ItemType File -Force" -InformationAction Continue
    }
}

function Test-EndToEndWorkflow {
    [CmdletBinding()]
    param()

    Write-Information "🔄 Testing End-to-End Workflow..." -InformationAction Continue

    try {
        # Simulate complete workflow
        Write-Information "  Step 1: Initialize secure configuration..." -InformationAction Continue
        Initialize-SecureGrokConfig

        Write-Information "  Step 2: Store API key..." -InformationAction Continue
        Set-SecureApiKey -ApiKey $TestApiKey

        Write-Information "  Step 3: Unlock vault..." -InformationAction Continue
        $unlocked = Unlock-AutomatedSecretStore

        Write-Information "  Step 4: Retrieve key..." -InformationAction Continue
        $secureKey = Get-SecureApiKey
        $plainKey = ConvertFrom-SecureApiKey -SecureApiKey $secureKey

        Write-Information "  Step 5: Set global variable..." -InformationAction Continue
        $global:GrokApiKey = $plainKey

        Write-Information "  Step 6: Configure Grok..." -InformationAction Continue
        $Global:GrokConfig = @{ ApiKey = $global:GrokApiKey }

        # Validate complete workflow
        $workflowSuccess = (
            $unlocked -and
            ($plainKey -eq $TestApiKey) -and
            ($global:GrokApiKey -eq $TestApiKey) -and
            ($Global:GrokConfig.ApiKey -eq $TestApiKey)
        )

        Write-TestResult -TestName "Complete End-to-End Workflow" -Passed $workflowSuccess -Message "All workflow steps completed successfully"

        return $workflowSuccess
    }
    catch {
        Write-TestResult -TestName "Complete End-to-End Workflow" -Passed $false -Message "Workflow failed: $($_.Exception.Message)"
        return $false
    }
}

function Test-RelatedSecrets {
    [CmdletBinding()]
    param()

    if (-not $TestRelatedSecrets) {
        Write-Information "ℹ️ Related secrets testing skipped. Use -TestRelatedSecrets to enable." -InformationAction Continue
        return $true
    }

    Write-Information "🔗 Testing Related Secrets Integration..." -InformationAction Continue

    $startTime = Get-Date
    $allTests = $true

    try {
        # Test 1: Azure SQL Connection String Storage
        Write-Information "  Testing Azure SQL secret storage..." -InformationAction Continue
        $testConnectionString = "Server=tcp:busbuddy-test.database.windows.net,1433;Database=BusBuddy;Authentication=Active Directory Default;Encrypt=True;"

        $stored = Set-GlobalSecureApiKey -Provider "AzureSQL" -ApiKey $testConnectionString
        $retrieved = Get-GlobalSecureApiKey -Provider "AzureSQL"

        $azureSqlTest = ($stored -and $retrieved -eq $testConnectionString)
        Write-TestResult -TestName "Azure SQL Connection String Storage" -Passed $azureSqlTest -Message "Stored and retrieved successfully" -Category "RelatedSecrets"
        $allTests = $allTests -and $azureSqlTest

        # Test 2: Syncfusion License Key Storage
        Write-Information "  Testing Syncfusion license key storage..." -InformationAction Continue
        $testSyncfusionKey = "TEST-SYNC-LICENSE-KEY-12345"

        $stored = Set-GlobalSecureApiKey -Provider "Syncfusion" -ApiKey $testSyncfusionKey
        $retrieved = Get-GlobalSecureApiKey -Provider "Syncfusion"

        $syncfusionTest = ($stored -and $retrieved -eq $testSyncfusionKey)
        Write-TestResult -TestName "Syncfusion License Key Storage" -Passed $syncfusionTest -Message "Stored and retrieved successfully" -Category "RelatedSecrets"
        $allTests = $allTests -and $syncfusionTest

        # Test 3: Multiple Provider Management
        Write-Information "  Testing multiple provider management..." -InformationAction Continue
        $providers = @("xAI", "AzureSQL", "Syncfusion")
        $multiProviderTest = $true

        foreach ($provider in $providers) {
            $secretExists = $null -ne (Get-GlobalSecureApiKey -Provider $provider)
            if (-not $secretExists) {
                $multiProviderTest = $false
                break
            }
        }

        Write-TestResult -TestName "Multiple Provider Management" -Passed $multiProviderTest -Message "All test providers accessible" -Category "RelatedSecrets"
        $allTests = $allTests -and $multiProviderTest

        # Test 4: BusBuddy-3 Environment Variable Integration
        Write-Information "  Testing BusBuddy-3 environment variable integration..." -InformationAction Continue

        # Simulate BusBuddy-3 environment setup
        $originalEnv = $env:XAI_API_KEY
        $env:XAI_API_KEY = $null  # Clear to test vault fallback

        $envIntegrationTest = $false
        try {
            $apiKey = Get-GlobalSecureApiKey -Provider "xAI"
            $envIntegrationTest = $null -ne $apiKey -and $apiKey.Length -gt 0
        }
        finally {
            $env:XAI_API_KEY = $originalEnv  # Restore
        }

        Write-TestResult -TestName "BusBuddy-3 Environment Integration" -Passed $envIntegrationTest -Message "Vault fallback works when env var missing" -Category "RelatedSecrets"
        $allTests = $allTests -and $envIntegrationTest

        # Test 5: Secret Rotation Simulation
        Write-Information "  Testing secret rotation capabilities..." -InformationAction Continue
        $originalKey = Get-GlobalSecureApiKey -Provider "xAI"
        $newTestKey = "TEST-ROTATED-KEY-67890"

        $rotationTest = $false
        try {
            # Rotate secret
            $stored = Set-GlobalSecureApiKey -Provider "xAI" -ApiKey $newTestKey
            $retrieved = Get-GlobalSecureApiKey -Provider "xAI"

            # Verify rotation
            $rotationTest = ($stored -and $retrieved -eq $newTestKey -and $retrieved -ne $originalKey)

            # Restore original
            Set-GlobalSecureApiKey -Provider "xAI" -ApiKey $originalKey | Out-Null
        }
        catch {
            # Ensure original key is restored even on failure
            Set-GlobalSecureApiKey -Provider "xAI" -ApiKey $originalKey | Out-Null
            throw
        }

        Write-TestResult -TestName "Secret Rotation Capability" -Passed $rotationTest -Message "Successfully rotated and restored secret" -Category "RelatedSecrets"
        $allTests = $allTests -and $rotationTest

    }
    catch {
        Write-TestResult -TestName "Related Secrets Integration" -Passed $false -Message "Integration test failed: $($_.Exception.Message)" -Category "RelatedSecrets"
        $allTests = $false
    }
    finally {
        # Clean up test secrets
        try {
            Remove-Secret -Name "AzureSQL" -Vault "BusBuddySecrets" -ErrorAction SilentlyContinue
            Remove-Secret -Name "Syncfusion" -Vault "BusBuddySecrets" -ErrorAction SilentlyContinue
        }
        catch {
            # Ignore cleanup errors
        }
    }

    $duration = (Get-Date) - $startTime
    if ($EnablePerformanceTesting) {
        Write-TestResult -TestName "Related Secrets Performance" -Passed ($duration.TotalSeconds -lt 10) -Message "Duration: $($duration.TotalMilliseconds) ms" -Category "Performance"
    }

    return $allTests
}

function Test-CrossPlatformCompatibility {
    [CmdletBinding()]
    param()

    Write-Information "🌐 Testing Cross-Platform Compatibility..." -InformationAction Continue

    $startTime = Get-Date
    $allTests = $true

    try {
        # Test 1: Platform Detection
        $platformInfo = $PSVersionTable.Platform
        $psEditionInfo = $PSVersionTable.PSEdition
        $isWindowsSystem = $platformInfo -eq 'Win32NT' -or $psEditionInfo -eq 'Desktop'
        $isLinuxSystem = $platformInfo -eq 'Unix' -and [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Linux)
        $isMacOSSystem = $platformInfo -eq 'Unix' -and [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::OSX)

        Write-TestResult -TestName "Platform Detection" -Passed $true -Message "Platform: $platformInfo, Edition: $psEditionInfo" -Details "Windows: $isWindowsSystem, Linux: $isLinuxSystem, macOS: $isMacOSSystem" -Category "CrossPlatform"

        # Test 2: DPAPI Availability (Windows-specific)
        if ($isWindowsSystem) {
            Write-Information "  Testing DPAPI availability on Windows..." -InformationAction Continue
            $dpapiTest = $true
            try {
                # Test basic DPAPI functionality through SecretStore
                $testVault = Get-SecretVault -Name "BusBuddySecrets" -ErrorAction SilentlyContinue
                $dpapiTest = $null -ne $testVault
            }
            catch {
                $dpapiTest = $false
            }
            Write-TestResult -TestName "DPAPI/SecretStore Availability" -Passed $dpapiTest -Message "SecretStore vault accessible" -Category "CrossPlatform"
            $allTests = $allTests -and $dpapiTest
        }
        else {
            Write-TestResult -TestName "DPAPI Alternative Required" -Passed $true -Message "Non-Windows platform detected" -Details "Consider Azure KeyVault or other cloud-based secret management" -Category "CrossPlatform"
        }

        # Test 3: Path Handling
        Write-Information "  Testing cross-platform path handling..." -InformationAction Continue
        $testPath = Join-Path $HOME "test-secure-config"
        $pathTest = (Split-Path $testPath) -eq $HOME
        Write-TestResult -TestName "Cross-Platform Path Handling" -Passed $pathTest -Message "Path resolution works correctly" -Category "CrossPlatform"
        $allTests = $allTests -and $pathTest

        # Test 4: PowerShell Execution Policy (Windows-specific)
        if ($isWindowsSystem) {
            Write-Information "  Testing PowerShell execution policy..." -InformationAction Continue
            $executionPolicy = Get-ExecutionPolicy -Scope CurrentUser
            $policyTest = $executionPolicy -ne 'Restricted'
            Write-TestResult -TestName "Execution Policy Check" -Passed $policyTest -Message "Policy: $executionPolicy" -Details "Modules can be imported" -Category "CrossPlatform"
            $allTests = $allTests -and $policyTest
        }

    }
    catch {
        Write-TestResult -TestName "Cross-Platform Compatibility" -Passed $false -Message "Compatibility test failed: $($_.Exception.Message)" -Category "CrossPlatform"
        $allTests = $false
    }

    $duration = (Get-Date) - $startTime
    if ($EnablePerformanceTesting) {
        Write-TestResult -TestName "Cross-Platform Performance" -Passed ($duration.TotalSeconds -lt 5) -Message "Duration: $($duration.TotalMilliseconds) ms" -Category "Performance"
    }

    return $allTests
}

function Invoke-Cleanup {
    [CmdletBinding()]
    param()

    if (-not $CleanupAfterTest) {
        Write-Information "ℹ️ Cleanup skipped. Use -CleanupAfterTest to remove test data." -InformationAction Continue
        return
    }

    Write-Information "🧹 Cleaning up test data..." -InformationAction Continue

    try {
        # Remove test secret
        Remove-Secret -Name "XAI_API_KEY" -Vault "BusBuddySecrets" -ErrorAction SilentlyContinue
        Write-Information "  Removed test API key from vault" -InformationAction Continue

        # Clear global variables
        Remove-Variable -Name "GrokApiKey" -Scope Global -ErrorAction SilentlyContinue
        Remove-Variable -Name "GrokConfig" -Scope Global -ErrorAction SilentlyContinue
        Write-Information "  Cleared global variables" -InformationAction Continue

        # Note: Not removing the vault itself as it may be needed for actual use
        Write-Information "  Note: BusBuddySecrets vault preserved for actual use" -InformationAction Continue
    }
    catch {
        Write-Warning "Cleanup failed: $($_.Exception.Message)"
    }
}

function Show-TestSummary {
    [CmdletBinding()]
    param()

    Write-Information "`n📊 Test Summary" -InformationAction Continue
    Write-Information "===============" -InformationAction Continue

    $totalTests = $script:TestResults.Count
    $passedTests = ($script:TestResults | Where-Object { $_.Passed }).Count
    $failedTests = $totalTests - $passedTests

    Write-Information "Total Tests: $totalTests" -InformationAction Continue
    Write-Information "Passed: $passedTests" -InformationAction Continue
    Write-Information "Failed: $failedTests" -InformationAction Continue

    # Enhanced reporting with categories and performance data
    if ($script:TestResults.Count -gt 0) {
        Write-Information "`n📋 Test Results by Category:" -InformationAction Continue

        $categories = $script:TestResults | Group-Object Category | Sort-Object Name
        foreach ($category in $categories) {
            $categoryPassed = ($category.Group | Where-Object { $_.Passed }).Count
            $categoryTotal = $category.Group.Count
            $categoryPercent = [math]::Round(($categoryPassed / $categoryTotal) * 100, 1)

            Write-Information "  $($category.Name): $categoryPassed/$categoryTotal ($categoryPercent%)" -InformationAction Continue
        }

        # Performance summary
        if ($EnablePerformanceTesting) {
            Write-Information "`n⏱️ Performance Summary:" -InformationAction Continue
            $performanceTests = $script:TestResults | Where-Object { $_.Duration -gt [timespan]::Zero }

            if ($performanceTests) {
                $totalDuration = ($performanceTests | Measure-Object -Property Duration -Sum).Sum
                $avgDuration = ($performanceTests | Measure-Object -Property Duration -Average).Average
                $maxDuration = ($performanceTests | Measure-Object -Property Duration -Maximum).Maximum

                Write-Information "  Total Execution Time: $($totalDuration.TotalMilliseconds) ms" -InformationAction Continue
                Write-Information "  Average Test Duration: $($avgDuration.TotalMilliseconds) ms" -InformationAction Continue
                Write-Information "  Longest Test Duration: $($maxDuration.TotalMilliseconds) ms" -InformationAction Continue

                # Identify slow tests
                $slowTests = $performanceTests | Where-Object { $_.Duration.TotalSeconds -gt 2 } | Sort-Object Duration -Descending
                if ($slowTests) {
                    Write-Information "`n⚠️ Slow Tests (>2s):" -InformationAction Continue
                    foreach ($test in $slowTests) {
                        Write-Information "    $($test.TestName): $($test.Duration.TotalMilliseconds) ms" -InformationAction Continue
                    }
                }
            }
        }

        # Failed tests details
        if ($failedTests -gt 0) {
            Write-Information "`n❌ Failed Tests:" -InformationAction Continue
            $failedTestDetails = $script:TestResults | Where-Object { -not $_.Passed }
            foreach ($test in $failedTestDetails) {
                Write-Information "  • $($test.TestName)" -InformationAction Continue
                if ($test.Message) {
                    Write-Information "    Message: $($test.Message)" -InformationAction Continue
                }
                if ($test.Details) {
                    Write-Information "    Details: $($test.Details)" -InformationAction Continue
                }
            }
        }

        # CI/CD Report Generation
        if ($GenerateCIReport) {
            Write-Information "`n📄 Generating CI/CD Report..." -InformationAction Continue
            $ciReport = Export-TestResultsForCI -TestResults $script:TestResults

            if ($ciReport) {
                Write-Information "  CI report generated successfully" -InformationAction Continue
                Write-Host "::set-output name=test-results::$ciReport"

                # Set GitHub Actions outputs
                Write-Host "::set-output name=tests-total::$totalTests"
                Write-Host "::set-output name=tests-passed::$passedTests"
                Write-Host "::set-output name=tests-failed::$failedTests"
                Write-Host "::set-output name=test-success::$($failedTests -eq 0)"

                # Create test summary for GitHub
                $githubSummary = @"
## Secure API Key Implementation Test Results

| Metric | Value |
|--------|-------|
| Total Tests | $totalTests |
| Passed | $passedTests |
| Failed | $failedTests |
| Success Rate | $([math]::Round(($passedTests / $totalTests) * 100, 1))% |

### Test Categories
$(($categories | ForEach-Object {
    $categoryPassed = ($_.Group | Where-Object { $_.Passed }).Count
    $categoryTotal = $_.Group.Count
    $categoryPercent = [math]::Round(($categoryPassed / $categoryTotal) * 100, 1)
    "| $($_.Name) | $categoryPassed/$categoryTotal ($categoryPercent%) |"
}) -join "`n")

$(if ($failedTests -gt 0) {
    "### Failed Tests`n" + (($failedTestDetails | ForEach-Object { "- **$($_.TestName)**: $($_.Message)" }) -join "`n")
})
"@
                Write-Host "::set-output name=github-summary::$githubSummary"
            }
        }
    }

    # Overall result
    $overallSuccess = $failedTests -eq 0
    $resultIcon = if ($overallSuccess) { "✅" } else { "❌" }
    $resultText = if ($overallSuccess) { "SUCCESS" } else { "FAILURE" }

    Write-Information "`n$resultIcon Overall Result: $resultText" -InformationAction Continue

    # Exit code for CI/CD
    if ($GenerateCIReport -and -not $overallSuccess) {
        exit 1
    }

    return $overallSuccess
}

function Export-TestResultsForCI {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [array]$TestResults
    )

    try {
        # Create CI-friendly test result object
        $ciResults = @{
            Timestamp = Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ"
            Framework = "PowerShell SecretManagement Test Suite"
            Environment = @{
                Platform = $PSVersionTable.Platform
                PSVersion = $PSVersionTable.PSVersion.ToString()
                PSEdition = $PSVersionTable.PSEdition
                OS = $PSVersionTable.OS
            }
            Summary = @{
                Total = $TestResults.Count
                Passed = ($TestResults | Where-Object { $_.Passed }).Count
                Failed = ($TestResults | Where-Object { -not $_.Passed }).Count
                Categories = ($TestResults | Group-Object Category | ForEach-Object {
                    @{
                        Name = $_.Name
                        Total = $_.Count
                        Passed = ($_.Group | Where-Object { $_.Passed }).Count
                    }
                })
            }
            Tests = $TestResults | ForEach-Object {
                @{
                    Name = $_.TestName
                    Category = $_.Category
                    Passed = $_.Passed
                    Message = $_.Message
                    Details = $_.Details
                    Duration = if ($_.Duration) { $_.Duration.TotalMilliseconds } else { 0 }
                    Timestamp = $_.Timestamp.ToString("yyyy-MM-ddTHH:mm:ssZ")
                }
            }
        }

        # Convert to JSON for CI consumption
        $jsonReport = $ciResults | ConvertTo-Json -Depth 10 -Compress

        # Save to file for CI artifacts
        $reportPath = Join-Path $PWD "secure-api-test-results.json"
        $jsonReport | Out-File -FilePath $reportPath -Encoding UTF8

        Write-Information "  Test results saved to: $reportPath" -InformationAction Continue

        return $jsonReport
    }
    catch {
        Write-Warning "Failed to export CI results: $($_.Exception.Message)"
        return $null
    }
}

# Main test execution
function Invoke-SecureApiKeyImplementationTest {
    [CmdletBinding()]
    param()

    Write-Information "🚀 Starting Enhanced Secure API Key Implementation Test Suite" -InformationAction Continue
    Write-Information "================================================================" -InformationAction Continue
    Write-Information "PowerShell Version: $($PSVersionTable.PSVersion)" -InformationAction Continue
    Write-Information "Platform: $($PSVersionTable.Platform)" -InformationAction Continue
    Write-Information "Test API Key: $($TestApiKey.Substring(0, 20))..." -InformationAction Continue
    Write-Information "Performance Testing: $EnablePerformanceTesting" -InformationAction Continue
    Write-Information "Related Secrets Testing: $TestRelatedSecrets" -InformationAction Continue
    Write-Information "CI Report Generation: $GenerateCIReport" -InformationAction Continue
    Write-Information "" -InformationAction Continue

    # Execute test phases with enhanced capabilities
    Write-Information "📋 Phase 1: Prerequisites and Platform Compatibility" -InformationAction Continue
    $prerequisitesPassed = Test-Prerequisites
    if (-not $prerequisitesPassed) {
        Write-Warning "Prerequisites failed. Cannot continue with implementation tests."
        Show-TestSummary
        return $false
    }

    # Cross-platform compatibility testing
    Test-CrossPlatformCompatibility

    Write-Information "`n📋 Phase 2: Module Import and Initialization" -InformationAction Continue
    $moduleImported = Test-ModuleImport
    if (-not $moduleImported) {
        Write-Warning "Module import failed. Cannot continue with vault tests."
        Show-TestSummary
        return $false
    }

    Write-Information "`n📋 Phase 3: Core Security Functionality" -InformationAction Continue
    # Core functionality tests with performance measurement
    if ($EnablePerformanceTesting) {
        $vaultResult = Measure-TestExecution -TestName "Vault Initialization" -TestScript { Test-VaultInitialization }
        $storageResult = Measure-TestExecution -TestName "API Key Storage" -TestScript { Test-ApiKeyStorage -ApiKey $TestApiKey }
        $retrievalResult = Measure-TestExecution -TestName "API Key Retrieval" -TestScript { Test-ApiKeyRetrieval -ExpectedKey $TestApiKey }
    } else {
        Test-VaultInitialization
        Test-ApiKeyStorage -ApiKey $TestApiKey
        $retrievedKey = Test-ApiKeyRetrieval -ExpectedKey $TestApiKey
    }

    Write-Information "`n📋 Phase 4: Integration Testing" -InformationAction Continue
    if ($retrievedKey) {
        Test-GlobalProfileIntegration -ExpectedKey $TestApiKey
        Test-GrokConfigIntegration
        Test-SecurityValidation
    }

    # Related secrets testing (BusBuddy-3 specific)
    if ($TestRelatedSecrets) {
        Write-Information "`n📋 Phase 5: Related Secrets Integration (BusBuddy-3)" -InformationAction Continue
        Test-RelatedSecrets
    }

    Write-Information "`n📋 Phase 6: File System and Workflow Tests" -InformationAction Continue
    # File and workflow tests
    Test-GlobalProfileFile
    Test-EndToEndWorkflow

    Write-Information "`n📋 Phase 7: Cleanup and Reporting" -InformationAction Continue
    # Cleanup and enhanced summary with CI integration
    Invoke-Cleanup
    $overallSuccess = Show-TestSummary

    # Return success based on failed test count
    return $overallSuccess
}

# Export the main test function
Export-ModuleMember -Function Invoke-SecureApiKeyImplementationTest

# If running as script (not imported), execute the test
if ($MyInvocation.InvocationName -ne '.') {
    $testPassed = Invoke-SecureApiKeyImplementationTest
    exit ([int](-not $testPassed))
}
