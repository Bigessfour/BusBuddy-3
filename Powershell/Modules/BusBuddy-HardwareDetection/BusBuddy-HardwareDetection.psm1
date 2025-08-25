# BusBuddy-HardwareDetection.psm1
# PowerShell module for hardware detection operations
#requires -Version 7.5

[CmdletBinding()]
param()


# Minimal function to prevent null script errors
function Get-HardwareInfo {
    [CmdletBinding()]
    param()
    Get-WmiObject Win32_ComputerSystem
}

Export-ModuleMember -Function Get-HardwareInfo
