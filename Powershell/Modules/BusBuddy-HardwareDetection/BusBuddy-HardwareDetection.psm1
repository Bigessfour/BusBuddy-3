# BusBuddy-HardwareDetection.psm1
# PowerShell module for hardware detection operations
#requires -Version 7.5

[CmdletBinding()]
param()


# Minimal function to prevent null script errors
<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.EXAMPLE
${3:An example}

.NOTES
${4:General notes}
#>
function Get-HardwareInfo {
    [CmdletBinding()]
    param()
    Get-WmiObject Win32_ComputerSystem
}

Export-ModuleMember -Function Get-HardwareInfo
