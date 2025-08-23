#requires -Module Pester

<#
.SYNOPSIS
    Pester tests for BusBuddy Grok Assistant CI Analysis modules

.DESCRIPTION
    Comprehensive test suite for validating CI analysis functionality,
    API integration, and pattern matching capabilities.
#>

Describe "BusBuddy Grok Assistant Integration Tests" {

    BeforeAll {
        # Import required modules
        Import-Module "$PSScriptRoot\..\Modules\BusBuddy-CIAnalysis.psm1" -Force
        Import-Module "$PSScriptRoot\..\Modules\BusBuddy-CIAnalysis-Enhanced.psm1" -Force

        # Mock environment variables for testing
        $env:XAI_API_KEY = "test-api-key-for-testing"

        # Test data
        $script:TestErrorMessages = @{
            CSharpError = "CS1061: 'string' does not contain a definition for 'Contains'"
            SyncfusionError = "Could not load file or assembly 'Syncfusion.SfGrid.WPF'"
            EntityFrameworkError = "Unable to create a 'DbContext' of type 'AppDbContext'"
            YamlError = "mapping values are not allowed here in '<unicode string>', line 15"
            BuildError = "Build FAILED. 2 error(s)"
        }
    }

    Context "Module Loading and Configuration" {

        It "Should load BusBuddy-CIAnalysis module successfully" {
            Get-Module BusBuddy-CIAnalysis | Should -Not -BeNullOrEmpty
        }

        It "Should load BusBuddy-CIAnalysis-Enhanced module successfully" {
            Get-Module BusBuddy-CIAnalysis-Enhanced | Should -Not -BeNullOrEmpty
        }

        It "Should export expected functions from base module" {
            $functions = Get-Command -Module BusBuddy-CIAnalysis
            $functions.Name | Should -Contain "Invoke-CIFailureAnalysis"
            $functions.Name | Should -Contain "Invoke-AIAnalysis"
        }

        It "Should export expected functions from enhanced module" {
            $functions = Get-Command -Module BusBuddy-CIAnalysis-Enhanced
            $functions.Name | Should -Contain "Invoke-EnhancedCIAnalysis"
            $functions.Name | Should -Contain "Start-AsyncCIAnalysis"
        }

        It "Should have proper aliases configured" {
            (Get-Alias ci-analyze -ErrorAction SilentlyContinue) | Should -Not -BeNullOrEmpty
            (Get-Alias ci-analyze-enhanced -ErrorAction SilentlyContinue) | Should -Not -BeNullOrEmpty
        }
    }

    Context "Pattern Analysis Testing" {

        It "Should correctly identify C# compilation errors" {
            $result = Invoke-AIAnalysis -InputText $TestErrorMessages.CSharpError -AnalysisType 'build-error'

            $result | Should -Not -BeNullOrEmpty
            $result.Severity | Should -Be "High"
            $result.Insights | Should -Contain "*C# Compilation Error*"
        }

        It "Should correctly identify Syncfusion-related issues" {
            $result = Invoke-AIAnalysis -InputText $TestErrorMessages.SyncfusionError -AnalysisType 'build-error'

            $result | Should -Not -BeNullOrEmpty
            $result.Insights -join " " | Should -Match "Syncfusion"
        }

        It "Should correctly identify Entity Framework issues" {
            $result = Invoke-AIAnalysis -InputText $TestErrorMessages.EntityFrameworkError -AnalysisType 'build-error'

            $result | Should -Not -BeNullOrEmpty
            $result.Insights -join " " | Should -Match "Entity Framework|Database"
        }

        It "Should correctly identify YAML syntax errors" {
            $result = Invoke-AIAnalysis -InputText $TestErrorMessages.YamlError -AnalysisType 'workflow-error'

            $result | Should -Not -BeNullOrEmpty
            $result.Severity | Should -Be "High"
            $result.Insights -join " " | Should -Match "YAML|syntax"
        }

        It "Should provide actionable recommendations" {
            $result = Invoke-AIAnalysis -InputText $TestErrorMessages.BuildError -AnalysisType 'ci-failure'

            $result.Recommendations | Should -Not -BeNullOrEmpty
            $result.Recommendations.Count | Should -BeGreaterThan 0
            $result.Recommendations[0] | Should -Match "✅"
        }
    }

    Context "Enhanced Analysis Features" {

        It "Should handle local build artifact detection" {
            # Mock build artifacts
            $tempDir = New-TemporaryFile | ForEach-Object { Remove-Item $_; New-Item -ItemType Directory -Path $_ }
            $tempLogFile = Join-Path $tempDir "build.binlog"
            "Build log content" | Out-File $tempLogFile

            Push-Location $tempDir
            try {
                $artifacts = Get-LocalBuildArtifacts
                $artifacts | Should -Not -BeNullOrEmpty
                $artifacts | Should -Match "build.binlog"
            } finally {
                Pop-Location
                Remove-Item $tempDir -Recurse -Force
            }
        }

        It "Should validate GitHub CLI integration capability" {
            $hasGitHubCli = Get-Command gh -ErrorAction SilentlyContinue

            if ($hasGitHubCli) {
                # Test GitHub data fetching (will fail without auth, but function should handle gracefully)
                { Get-GitHubActionsData } | Should -Not -Throw
            } else {
                Write-Warning "GitHub CLI not available for testing"
            }
        }

        It "Should support asynchronous analysis" {
            $testData = @("Test error message", "Additional context")
            $testContext = @{ ProjectType = "Test" }

            $job = Start-AsyncCIAnalysis -Data $testData -Context $testContext

            $job | Should -Not -BeNullOrEmpty
            $job.GetType().Name | Should -Be "ThreadJob"

            # Clean up
            $job | Stop-Job -PassThru | Remove-Job
        }
    }

    Context "API Integration Mocking" {

        BeforeEach {
            # Mock Grok API calls to avoid actual API usage during tests
            Mock Invoke-GrokCIAnalysis {
                return @{
                    Analysis = "Mocked Grok analysis result for: $ErrorMessage"
                    Timestamp = Get-Date
                    ApiCallsUsed = 1
                }
            } -ModuleName BusBuddy-CIAnalysis-Enhanced
        }

        It "Should handle Grok API integration gracefully" {
            $result = Invoke-EnhancedCIAnalysis -ErrorMessage "Test error"

            $result | Should -Not -BeNullOrEmpty
            $result.Summary | Should -Match "completed"
        }

        It "Should fall back to pattern analysis when Grok API fails" {
            # Mock API failure
            Mock Invoke-GrokCIAnalysis {
                throw "API connection failed"
            } -ModuleName BusBuddy-CIAnalysis-Enhanced

            $result = Invoke-EnhancedCIAnalysis -ErrorMessage $TestErrorMessages.CSharpError

            $result | Should -Not -BeNullOrEmpty
            $result.AnalysisType | Should -Be "Enhanced Pattern-Based"
        }
    }

    Context "Configuration Validation" {

        It "Should validate model version consistency" {
            # Check grok-config.ps1
            $configContent = Get-Content "$PSScriptRoot\..\Modules\grok-config.ps1" -Raw
            $configContent | Should -Match 'DefaultModel = "grok-4-latest"'

            # Check .grok-config.json
            $jsonConfig = Get-Content "$PSScriptRoot\..\Modules\.grok-config.json" | ConvertFrom-Json
            $jsonConfig.grok_assistant.api_configuration.default_model | Should -Be "grok-4-latest"
        }

        It "Should have valid environment variable configuration" {
            $configContent = Get-Content "$PSScriptRoot\..\Modules\grok-config.ps1" -Raw
            $configContent | Should -Match 'XAI_API_KEY'
            $configContent | Should -Match 'GROK_API_KEY'
        }

        It "Should validate appsettings.json model configuration" {
            $appsettings = Get-Content "$PSScriptRoot\..\..\appsettings.json" | ConvertFrom-Json
            $appsettings.XAI.DefaultModel | Should -Be "grok-4-latest"
        }
    }

    Context "Database Optimization Integration" {

        It "Should have database optimization function available" {
            # Check if Invoke-GrokDatabaseOptimization exists in the assistant module
            $assistantFunctions = Get-Content "$PSScriptRoot\..\Modules\BusBuddy-GrokAssistant.psd1" -Raw
            $assistantFunctions | Should -Match "Invoke-GrokDatabaseOptimization"
        }

        It "Should validate Azure SQL specific prompts" {
            $configContent = Get-Content "$PSScriptRoot\..\Modules\grok-config.ps1" -Raw
            $configContent | Should -Match "Entity Framework|Azure SQL|query plan"
        }
    }

    Context "Performance and Reliability" {

        It "Should handle empty input gracefully" {
            { Invoke-AIAnalysis -InputText "" -AnalysisType 'ci-failure' } | Should -Not -Throw
        }

        It "Should handle very long input without crashing" {
            $longInput = "Error message " * 1000
            { Invoke-AIAnalysis -InputText $longInput -AnalysisType 'ci-failure' } | Should -Not -Throw
        }

        It "Should provide reasonable response times for pattern analysis" {
            $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
            Invoke-AIAnalysis -InputText $TestErrorMessages.CSharpError -AnalysisType 'build-error'
            $stopwatch.Stop()

            $stopwatch.ElapsedMilliseconds | Should -BeLessThan 5000  # Should complete within 5 seconds
        }
    }

    AfterAll {
        # Clean up environment variables
        Remove-Item Env:XAI_API_KEY -ErrorAction SilentlyContinue

        # Remove modules
        Remove-Module BusBuddy-CIAnalysis -Force -ErrorAction SilentlyContinue
        Remove-Module BusBuddy-CIAnalysis-Enhanced -Force -ErrorAction SilentlyContinue
    }
}

Describe "Grok Assistant Configuration Tests" {

    Context "Configuration File Validation" {

        It "Should have valid JSON configuration" {
            $configPath = "$PSScriptRoot\..\Modules\.grok-config.json"
            Test-Path $configPath | Should -Be $true

            { Get-Content $configPath | ConvertFrom-Json } | Should -Not -Throw
        }

        It "Should have valid PowerShell configuration" {
            $configPath = "$PSScriptRoot\..\Modules\grok-config.ps1"
            Test-Path $configPath | Should -Be $true

            { . $configPath } | Should -Not -Throw
        }

        It "Should have consistent API endpoints" {
            $jsonConfig = Get-Content "$PSScriptRoot\..\Modules\.grok-config.json" | ConvertFrom-Json
            $jsonConfig.grok_assistant.api_configuration.base_url | Should -Be "https://api.x.ai/v1"
        }
    }

    Context "Module Manifest Validation" {

        It "Should have valid module manifest" {
            $manifestPath = "$PSScriptRoot\..\Modules\BusBuddy-GrokAssistant.psd1"
            Test-Path $manifestPath | Should -Be $true

            { Test-ModuleManifest $manifestPath } | Should -Not -Throw
        }

        It "Should export all declared functions" {
            $manifest = Import-PowerShellDataFile "$PSScriptRoot\..\Modules\BusBuddy-GrokAssistant.psd1"
            $manifest.FunctionsToExport | Should -Not -BeNullOrEmpty
            $manifest.FunctionsToExport.Count | Should -BeGreaterThan 10
        }
    }
}
