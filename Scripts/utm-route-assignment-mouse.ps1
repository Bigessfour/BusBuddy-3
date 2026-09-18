#Requires -Version 5.1
<#
.SYNOPSIS
  Interactive-session UIA mouse smoke for Route Assignments dock pane.

.DESCRIPTION
  Run via schtasks /IT on the UTM guest (not plain SSH). Clicks Syncfusion ButtonAdv
  controls by AutomationProperties.Name and logs to %TEMP%\busbuddy-uia-route.out.
#>
param(
    [int]$StartupWaitSeconds = 12,
    [string]$LogPath = "$env:TEMP\busbuddy-uia-route-$PID.log"
)

$ErrorActionPreference = 'Continue'
"" | Set-Content -Path $LogPath -Encoding utf8

function Log([string]$Message) {
    $line = "{0} {1}" -f (Get-Date -Format 'HH:mm:ss'), $Message
    Add-Content -Path $LogPath -Value $line -Encoding utf8
    Write-Host $line
}

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class RouteMouseClicker {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint x, uint y, uint d, UIntPtr e);
  public static void Click(int x, int y) {
    SetCursorPos(x, y);
    mouse_event(2, 0, 0, 0, UIntPtr.Zero);
    mouse_event(4, 0, 0, 0, UIntPtr.Zero);
  }
}
"@

function Find-ByName($root, [string]$name) {
    if ($null -eq $root) { return $null }
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, $name)
    return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
}

function Click-AutomationName([string]$name, [int]$pauseMs = 900) {
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $el = Find-ByName $root $name
    if ($null -eq $el) {
        Log "MISS name=$name"
        return $false
    }

    try {
        $pt = $el.GetClickablePoint()
        [RouteMouseClicker]::Click([int]$pt.X, [int]$pt.Y)
        Log "CLICK name=$name at=$([int]$pt.X),$([int]$pt.Y)"
        Start-Sleep -Milliseconds $pauseMs
        return $true
    } catch {
        $rect = $el.Current.BoundingRectangle
        if ($rect.Width -gt 0 -and $rect.Height -gt 0) {
            $x = [int]($rect.X + ($rect.Width / 2))
            $y = [int]($rect.Y + ($rect.Height / 2))
            [RouteMouseClicker]::Click($x, $y)
            Log "CLICK-FALLBACK name=$name at=$x,$y"
            Start-Sleep -Milliseconds $pauseMs
            return $true
        }

        Log "FAIL name=$name err=$($_.Exception.Message)"
        return $false
    }
}

function Wait-MainWindow([int]$timeoutSec = 45) {
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    while ((Get-Date) -lt $deadline) {
        $root = [System.Windows.Automation.AutomationElement]::RootElement
        $cond = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::NameProperty,
            'BusBuddy - School Transportation')
        $win = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
        if ($null -ne $win) {
            Log "WINDOW ok"
            return $win
        }
        Start-Sleep -Seconds 1
    }
    Log "WINDOW timeout"
    return $null
}

Log "START pid=$PID session=$([System.Diagnostics.Process]::GetCurrentProcess().SessionId)"
Start-Sleep -Seconds $StartupWaitSeconds

$win = Wait-MainWindow
if ($null -eq $win) { exit 2 }

# Activate Route Assignments dock tab (header from MainWindow.xaml DockingManager.Header)
Click-AutomationName 'Route Assignments' 1200 | Out-Null

$targets = @(
    'Refresh Route Data',
    'View Schedule',
    'Time Route',
    'Plot Route on Map',
    'Refresh Drive Path',
    'Generate Route Assignment Report',
    'Assign Bus to Route',
    'Assign Driver to Route',
    'Save Route',
    'Assign Student',
    'Remove Student',
    'Not riding today',
    'Add Stop',
    'Unassigned Students',
    'Assigned to Route',
    'Route Stops'
)

$ok = 0
foreach ($t in $targets) {
    if (Click-AutomationName $t) { $ok++ }
}

# Close schedule window if View Schedule opened one
$schedule = Find-ByName ([System.Windows.Automation.AutomationElement]::RootElement) 'Route Schedule'
if ($null -ne $schedule) {
    $close = Find-ByName $schedule 'Close'
    if ($null -ne $close) {
        try {
            $pt = $close.GetClickablePoint()
            [RouteMouseClicker]::Click([int]$pt.X, [int]$pt.Y)
            Log 'CLOSE schedule'
        } catch { Log 'CLOSE schedule skipped' }
    }
}

Log "DONE clicks=$ok/$($targets.Count)"
exit 0
