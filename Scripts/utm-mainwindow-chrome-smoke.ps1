#Requires -Version 5.1
<#
.SYNOPSIS
  Click MainWindow header nav buttons via mouse (ButtonAdv has no InvokePattern).

.DESCRIPTION
  Run via schtasks /IT after BusBuddy is launched. Uses AutomationProperties.Name
  from MainWindow.xaml. Closes each modal host when a new window appears.
#>
param(
    [int]$StartupWaitSeconds = 5,
    [string]$LogPath = "$env:TEMP\busbuddy-uia-mainchrome-$PID.log"
)

$ErrorActionPreference = 'Continue'
'' | Set-Content -Path $LogPath -Encoding utf8

function Log([string]$Message) {
    $line = '{0} {1}' -f (Get-Date -Format 'HH:mm:ss'), $Message
    Add-Content -Path $LogPath -Value $line -Encoding utf8
    Write-Host $line
}

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class MainChromeClicker {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint x, uint y, uint d, UIntPtr e);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
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

function Wait-MainWindow([int]$timeoutSec = 60) {
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    while ((Get-Date) -lt $deadline) {
        $root = [System.Windows.Automation.AutomationElement]::RootElement
        $cond = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::NameProperty,
            'BusBuddy - School Transportation')
        $win = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
        if ($null -ne $win) { return $win }
        Start-Sleep -Seconds 1
    }
    return $null
}

function Get-WindowElements {
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Window)
    # Children + descendants — WPF ShowDialog hosts sometimes nest under the owner.
    return @($root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)) +
           @($root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond))
}

function Get-TopWindowNames {
    $names = @()
    foreach ($w in (Get-WindowElements)) {
        if (-not [string]::IsNullOrWhiteSpace($w.Current.Name) -and $names -notcontains $w.Current.Name) {
            $names += $w.Current.Name
        }
    }
    return $names
}

function Find-WindowLike([string]$needle) {
    foreach ($w in (Get-WindowElements)) {
        $nm = $w.Current.Name
        if ([string]::IsNullOrWhiteSpace($nm)) { continue }
        if ($nm -eq 'BusBuddy - School Transportation') { continue }
        if ($nm -like "*$needle*") { return $w }
    }
    return $null
}

function Close-OwnedDialogs {
    [System.Windows.Forms.SendKeys]::SendWait('{ESC}')
    Start-Sleep -Milliseconds 400
    foreach ($w in (Get-WindowElements)) {
        $nm = $w.Current.Name
        if ([string]::IsNullOrWhiteSpace($nm)) { continue }
        if ($nm -eq 'BusBuddy - School Transportation') { continue }
        if ($nm -like '*powershell*' -or $nm -like '*cmd.exe*') { continue }
        try {
            Focus-Window $w
            [System.Windows.Forms.SendKeys]::SendWait('{ESC}')
            Start-Sleep -Milliseconds 250
            $wp = $w.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern)
            $wp.Close()
            Log "CLOSE window=$nm"
        }
        catch {
            Log ("CLOSE_SKIP window={0}" -f $nm)
        }
    }
    Start-Sleep -Milliseconds 600
}

function Focus-Window($win) {
    try {
        $hwnd = [IntPtr]$win.Current.NativeWindowHandle
        [MainChromeClicker]::ShowWindow($hwnd, 9) | Out-Null
        [MainChromeClicker]::SetForegroundWindow($hwnd) | Out-Null
    }
    catch {
        Log ("FOCUS_FAIL err={0}" -f $_.Exception.Message)
    }
}

function Click-AutomationName($root, [string]$name, [int]$pauseMs = 1100) {
    $el = Find-ByName $root $name
    if ($null -eq $el) {
        Log "MISS name=$name"
        return $false
    }
    if (-not $el.Current.IsEnabled) {
        Log "DISABLED name=$name"
        return $false
    }
    try {
        $pt = $el.GetClickablePoint()
        [MainChromeClicker]::Click([int]$pt.X, [int]$pt.Y)
        Log ("CLICK name={0} at={1},{2}" -f $name, [int]$pt.X, [int]$pt.Y)
        Start-Sleep -Milliseconds $pauseMs
        return $true
    }
    catch {
        $rect = $el.Current.BoundingRectangle
        if ($rect.Width -gt 0 -and $rect.Height -gt 0) {
            $x = [int]($rect.X + ($rect.Width / 2))
            $y = [int]($rect.Y + ($rect.Height / 2))
            [MainChromeClicker]::Click($x, $y)
            Log ("CLICK-FALLBACK name={0} at={1},{2}" -f $name, $x, $y)
            Start-Sleep -Milliseconds $pauseMs
            return $true
        }
        Log ("FAIL name={0} err={1}" -f $name, $_.Exception.Message)
        return $false
    }
}

function Close-ExtraWindows([string[]]$before) {
    Start-Sleep -Milliseconds 600
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Window)
    $wins = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
    foreach ($w in $wins) {
        $nm = $w.Current.Name
        if ([string]::IsNullOrWhiteSpace($nm)) { continue }
        if ($before -contains $nm) { continue }
        if ($nm -eq 'BusBuddy - School Transportation') { continue }
        Log "CLOSE extra window=$nm"
        try {
            Focus-Window $w
            [System.Windows.Forms.SendKeys]::SendWait('{ESC}')
            Start-Sleep -Milliseconds 400
            $wp = $w.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern)
            $wp.Close()
        }
        catch {
            try {
                $wp = $w.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern)
                $wp.Close()
            }
            catch {
                Log ("CLOSE_FAIL window={0} err={1}" -f $nm, $_.Exception.Message)
            }
        }
        Start-Sleep -Milliseconds 500
    }
}

Log ("START pid={0} session={1} log={2}" -f $PID, [System.Diagnostics.Process]::GetCurrentProcess().SessionId, $LogPath)
Start-Sleep -Seconds $StartupWaitSeconds
$main = Wait-MainWindow
if ($null -eq $main) {
    Log 'FAIL no MainWindow'
    exit 2
}
Log ("MAIN found name={0}" -f $main.Current.Name)
Focus-Window $main
# Clear startup Dashboard modal if user-settings DashboardOnStartup left it open.
Close-OwnedDialogs
$main = Wait-MainWindow 20
Focus-Window $main

$nav = @(
    @{ Name = 'Dashboard'; ExpectWindow = $true; Detail = 'Dashboard' },
    @{ Name = 'Map View Navigation'; ExpectWindow = $false; Detail = 'Map' },
    @{ Name = 'Students Management'; ExpectWindow = $true; Detail = 'Student' },
    @{ Name = 'Route Assignment'; ExpectWindow = $true; Detail = 'Route Management' },
    @{ Name = 'Bus Management'; ExpectWindow = $true; Detail = 'Vehicle' },
    @{ Name = 'Driver Management'; ExpectWindow = $true; Detail = 'Driver' },
    @{ Name = 'Fuel Management'; ExpectWindow = $true; Detail = 'Fuel' },
    @{ Name = 'Fleet Maintenance'; ExpectWindow = $true; Detail = 'Maintenance' },
    @{ Name = 'Reports and Print Routes'; ExpectWindow = $true; Detail = 'Report' },
    @{ Name = 'Trip Board'; ExpectWindow = $true; Detail = 'Trip' },
    @{ Name = 'Settings'; ExpectWindow = $true; Detail = 'Settings' }
)

$noop = New-Object System.Collections.Generic.List[string]
$ok = New-Object System.Collections.Generic.List[string]

foreach ($item in $nav) {
    Close-OwnedDialogs
    $main = Wait-MainWindow 20
    if ($null -eq $main) {
        Log 'FAIL MainWindow lost mid-run'
        break
    }
    Focus-Window $main
    $beforeNames = @(Get-TopWindowNames)
    Log ("CLICK begin name={0} windows={1} [{2}]" -f $item.Name, $beforeNames.Count, ($beforeNames -join ' | '))
    if (-not (Click-AutomationName $main $item.Name 1600)) {
        $noop.Add("$($item.Name): miss-or-fail")
        continue
    }

    $hitWin = $null
    if ($item.ExpectWindow) {
        $deadline = (Get-Date).AddSeconds(5)
        while ((Get-Date) -lt $deadline) {
            $hitWin = Find-WindowLike $item.Detail
            if ($null -ne $hitWin) { break }
            Start-Sleep -Milliseconds 250
        }
        if ($null -ne $hitWin) {
            $ok.Add("$($item.Name): opened $($hitWin.Current.Name)")
            Log ("OPENED name={0} window={1}" -f $item.Name, $hitWin.Current.Name)
        }
        else {
            $noop.Add("$($item.Name): no dialog (suspected no-op)")
            Log "NOOP suspected name=$($item.Name)"
        }
    }
    else {
        $ok.Add("$($item.Name): clicked (dock activate)")
    }

    Close-OwnedDialogs
}

Log '==== SUMMARY OK ===='
foreach ($line in $ok) { Log "OK $line" }
Log '==== SUMMARY NOOP ===='
if ($noop.Count -eq 0) { Log 'NOOP none' }
else { foreach ($line in $noop) { Log "NOOP $line" } }
Log ("DONE ok={0} noop={1} path={2}" -f $ok.Count, $noop.Count, $LogPath)
Write-Output "LOG=$LogPath"
exit 0
