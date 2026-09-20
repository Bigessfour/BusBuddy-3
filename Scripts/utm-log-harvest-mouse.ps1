#Requires -Version 5.1
<#
.SYNOPSIS
  Interactive-session UIA for the 2026-09-18 guest log harvest.

.DESCRIPTION
  Run via schtasks /IT (not plain SSH). Settings is a modal ShowDialog host.
  Save depot/bbox, then Close that window before Route Assignment / Add Stop /
  Activity Timeline. Does not click Generate Routes or District Map Export/Move.
#>
param(
    [int]$StartupWaitSeconds = 5,
    [switch]$SkipSettings,
    [string]$LogPath = "$env:TEMP\busbuddy-uia-harvest-$PID.log"
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
public class HarvestMouseClicker {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint x, uint y, uint d, UIntPtr e);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
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

function Find-WindowContains([string]$needle) {
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Window)
    $wins = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
    foreach ($w in $wins) {
        if ($w.Current.Name -like "*$needle*") { return $w }
    }
    $mainCond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty,
        'BusBuddy - School Transportation')
    $main = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $mainCond)
    if ($null -ne $main) {
        $nested = $main.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
        foreach ($w in $nested) {
            if ($w.Current.Name -like "*$needle*") { return $w }
        }
    }
    return $null
}

function Get-AncestorWindow($el) {
    if ($null -eq $el) { return $null }
    $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
    $cur = $el
    while ($null -ne $cur) {
        if ($cur.Current.ControlType -eq [System.Windows.Automation.ControlType]::Window) {
            return $cur
        }
        $cur = $walker.GetParent($cur)
    }
    return $null
}

function Dump-Windows {
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $kids = $root.FindAll(
        [System.Windows.Automation.TreeScope]::Children,
        [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($w in $kids) {
        Log ("TOP name={0} type={1} class={2} enabled={3}" -f $w.Current.Name, $w.Current.ControlType.ProgrammaticName, $w.Current.ClassName, $w.Current.IsEnabled)
    }
}

function Dump-Named($win, [int]$max = 70) {
    if ($null -eq $win) {
        Log 'DUMP skip (no window)'
        return
    }
    Log ("DUMP window={0}" -f $win.Current.Name)
    $types = @(
        [System.Windows.Automation.ControlType]::Button,
        [System.Windows.Automation.ControlType]::ComboBox,
        [System.Windows.Automation.ControlType]::Edit,
        [System.Windows.Automation.ControlType]::Text,
        [System.Windows.Automation.ControlType]::Custom
    )
    $n = 0
    foreach ($t in $types) {
        $cond = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty, $t)
        $els = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
        foreach ($el in $els) {
            $nm = $el.Current.Name
            if ([string]::IsNullOrWhiteSpace($nm)) { continue }
            Log ("  {0} name={1} offscreen={2} enabled={3}" -f $el.Current.ControlType.ProgrammaticName, $nm, $el.Current.IsOffscreen, $el.Current.IsEnabled)
            $n++
            if ($n -ge $max) {
                Log "DUMP truncated"
                return
            }
        }
    }
}

function Scroll-WindowBottom($win) {
    if ($null -eq $win) { return $false }
    try {
        $cond = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::IsScrollPatternAvailableProperty, $true)
        $el = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
        if ($null -eq $el) {
            Log 'SCROLL none'
            return $false
        }
        $sp = $el.GetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern)
        if ($sp.Current.VerticallyScrollable) {
            $sp.SetScrollPercent([System.Windows.Automation.ScrollPattern]::NoScroll, 100)
            Log 'SCROLL vertical=100'
            Start-Sleep -Milliseconds 400
            return $true
        }
        Log 'SCROLL not vertically scrollable'
        return $false
    } catch {
        Log "SCROLL err=$($_.Exception.Message)"
        return $false
    }
}

function Close-WindowContains([string]$needle) {
    $win = Find-WindowContains $needle
    if ($null -eq $win) {
        Log "CLOSE miss needle=$needle"
        return $false
    }
    try {
        $hwnd = [IntPtr]$win.Current.NativeWindowHandle
        if ($hwnd -ne [IntPtr]::Zero) { [HarvestMouseClicker]::SetForegroundWindow($hwnd) | Out-Null }
        $wp = $win.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern)
        $wp.Close()
        Log "CLOSE needle=$needle title=$($win.Current.Name)"
        Start-Sleep -Milliseconds 700
        return $true
    } catch {
        Log "CLOSE fail needle=$needle err=$($_.Exception.Message)"
        try {
            [System.Windows.Forms.SendKeys]::SendWait('%{F4}')
            Log "CLOSE AltF4 needle=$needle"
            Start-Sleep -Milliseconds 700
            return $true
        } catch {
            return $false
        }
    }
}

function Click-AutomationName([string]$name, [int]$pauseMs = 900, $root = $null) {
    if ($null -eq $root) { $root = [System.Windows.Automation.AutomationElement]::RootElement }
    $el = Find-ByName $root $name
    if ($null -eq $el) {
        Log "MISS name=$name"
        return $false
    }

    try {
        $inv = $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
        if ($null -ne $inv) {
            $inv.Invoke()
            Log "INVOKE name=$name"
            Start-Sleep -Milliseconds $pauseMs
            return $true
        }
    } catch { }

    try {
        try {
            $sip = $el.GetCurrentPattern([System.Windows.Automation.ScrollItemPattern]::Pattern)
            if ($null -ne $sip) { $sip.ScrollIntoView() }
        } catch { }
        $hwnd = [IntPtr]$el.Current.NativeWindowHandle
        if ($hwnd -ne [IntPtr]::Zero) { [HarvestMouseClicker]::SetForegroundWindow($hwnd) | Out-Null }
        $pt = $el.GetClickablePoint()
        [HarvestMouseClicker]::Click([int]$pt.X, [int]$pt.Y)
        Log "CLICK name=$name at=$([int]$pt.X),$([int]$pt.Y)"
        Start-Sleep -Milliseconds $pauseMs
        return $true
    } catch {
        $rect = $el.Current.BoundingRectangle
        if ($rect.Width -gt 0 -and $rect.Height -gt 0) {
            $x = [int]($rect.X + ($rect.Width / 2))
            $y = [int]($rect.Y + ($rect.Height / 2))
            [HarvestMouseClicker]::Click($x, $y)
            Log "CLICK-FALLBACK name=$name at=$x,$y"
            Start-Sleep -Milliseconds $pauseMs
            return $true
        }

        Log "FAIL name=$name err=$($_.Exception.Message)"
        return $false
    }
}

function Set-AutomationName([string]$name, [string]$value, $root = $null) {
    if ($null -eq $root) { $root = [System.Windows.Automation.AutomationElement]::RootElement }
    $el = Find-ByName $root $name
    if ($null -eq $el) {
        Log "MISS set name=$name"
        return $false
    }

    try {
        $pattern = $el.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
        if ($null -ne $pattern) {
            $pattern.SetValue($value)
            Log "SET name=$name via=ValuePattern"
            return $true
        }
    } catch { }

    if (-not (Click-AutomationName $name 400 $root)) { return $false }
    [System.Windows.Forms.SendKeys]::SendWait('^a')
    Start-Sleep -Milliseconds 80
    [System.Windows.Forms.SendKeys]::SendWait($value)
    Log "SET name=$name via=SendKeys"
    Start-Sleep -Milliseconds 250
    return $true
}

function Name-Exists([string]$name, $root = $null) {
    if ($null -eq $root) { $root = [System.Windows.Automation.AutomationElement]::RootElement }
    $el = Find-ByName $root $name
    if ($null -eq $el) {
        Log "ABSENT name=$name"
        return $false
    }
    Log ("PRESENT name=$name offscreen={0} enabled={1}" -f $el.Current.IsOffscreen, $el.Current.IsEnabled)
    return $true
}

function Wait-WindowContains([string]$needle, [int]$timeoutSec = 12) {
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    while ((Get-Date) -lt $deadline) {
        $win = Find-WindowContains $needle
        if ($null -ne $win) {
            Log ("WINDOW hit needle=$needle title={0}" -f $win.Current.Name)
            return $win
        }
        Start-Sleep -Milliseconds 400
    }
    Log "WINDOW timeout needle=$needle"
    return $null
}

function Wait-MainWindow([int]$timeoutSec = 50) {
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    while ((Get-Date) -lt $deadline) {
        $root = [System.Windows.Automation.AutomationElement]::RootElement
        $cond = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::NameProperty,
            'BusBuddy - School Transportation')
        $win = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
        if ($null -ne $win) {
            Log 'WINDOW ok'
            return $win
        }
        Start-Sleep -Seconds 1
    }
    Log 'WINDOW timeout'
    return $null
}

function Focus-Main {
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty,
        'BusBuddy - School Transportation')
    $main = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
    if ($null -eq $main) { return }
    $hwnd = [IntPtr]$main.Current.NativeWindowHandle
    if ($hwnd -ne [IntPtr]::Zero) { [HarvestMouseClicker]::SetForegroundWindow($hwnd) | Out-Null }
    Log 'FOCUS main'
    Start-Sleep -Milliseconds 400
}

function Close-LeftoverModals {
    foreach ($needle in @('New trip', 'Trip Board', 'Route Stop', 'Settings', 'Route Management', 'Activity Timeline')) {
        if ($null -ne (Find-WindowContains $needle)) {
            Close-WindowContains $needle | Out-Null
        }
    }
}

Log "START pid=$PID session=$([System.Diagnostics.Process]::GetCurrentProcess().SessionId) skipSettings=$SkipSettings"
Start-Sleep -Seconds $StartupWaitSeconds

$win = Wait-MainWindow
if ($null -eq $win) { Dump-Windows; exit 2 }
Dump-Windows
Close-LeftoverModals
Focus-Main
Dump-Windows

# --- Settings depot/bbox save (modal host; must Close before other clicks) ---
if ($SkipSettings) {
    Log 'SKIP Settings (already saved this session)'
} else {
    $settings = Find-WindowContains 'Settings'
    if ($null -eq $settings) {
        $lat = Find-ByName ([System.Windows.Automation.AutomationElement]::RootElement) 'Bus barn latitude'
        if ($null -ne $lat) {
            $settings = Get-AncestorWindow $lat
            if ($null -ne $settings) { Log ("Settings leftover via ancestor title={0}" -f $settings.Current.Name) }
        }
    }
    if ($null -eq $settings) {
        Click-AutomationName 'Settings' 1600 | Out-Null
        $settings = Wait-WindowContains 'Settings' 10
        if ($null -eq $settings) {
            $lat = Find-ByName ([System.Windows.Automation.AutomationElement]::RootElement) 'Bus barn latitude'
            $settings = Get-AncestorWindow $lat
            if ($null -ne $settings) { Log ("Settings via ancestor after click title={0}" -f $settings.Current.Name) }
        }
    }
    Dump-Windows
    if ($null -ne $settings) {
        Log ("Settings host title={0} class={1}" -f $settings.Current.Name, $settings.Current.ClassName)
        Set-AutomationName 'Bus barn latitude' '38.0872' $settings | Out-Null
        Set-AutomationName 'Bus barn longitude' '-102.6208' $settings | Out-Null
        Set-AutomationName 'District bounding box south latitude' '38.0500' $settings | Out-Null
        Set-AutomationName 'District bounding box west longitude' '-102.8000' $settings | Out-Null
        Set-AutomationName 'District bounding box north latitude' '38.2500' $settings | Out-Null
        Set-AutomationName 'District bounding box east longitude' '-102.4000' $settings | Out-Null
        Scroll-WindowBottom $settings | Out-Null
        Click-AutomationName 'Save settings' 1800 $settings | Out-Null
        Start-Sleep -Seconds 1
        try {
            $wp = $settings.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern)
            $wp.Close()
            Log 'CLOSE Settings via WindowPattern'
            Start-Sleep -Milliseconds 800
        } catch {
            Close-WindowContains 'Settings' | Out-Null
            Start-Sleep -Milliseconds 800
        }
    } else {
        Log 'FAIL no Settings window'
        $saveEl = Find-ByName ([System.Windows.Automation.AutomationElement]::RootElement) 'Save settings'
        if ($null -ne $saveEl) {
            Log ("Save settings offscreen={0} rect={1}" -f $saveEl.Current.IsOffscreen, $saveEl.Current.BoundingRectangle)
            Scroll-WindowBottom (Get-AncestorWindow $saveEl) | Out-Null
            Click-AutomationName 'Save settings' 1800 | Out-Null
        }
    }
}

Dump-Windows

# --- Add Stop dialog (DataContext proof). Do not click ribbon "Route Assignment"
# (that name is on Routes -> Route Management, a modal that blocks the dock). ---
Focus-Main
Click-AutomationName 'Route Assignments' 1400 | Out-Null
$addStop = Find-ByName ([System.Windows.Automation.AutomationElement]::RootElement) 'Add Stop'
if ($null -ne $addStop) {
    Log ("Add Stop offscreen={0} enabled={1} rect={2}" -f $addStop.Current.IsOffscreen, $addStop.Current.IsEnabled, $addStop.Current.BoundingRectangle)
} else {
    Log 'MISS dump Add Stop'
}
Click-AutomationName 'Add Stop' 1800 | Out-Null
$stopWin = Wait-WindowContains 'Route Stop' 8
Dump-Windows
if ($null -ne $stopWin) {
    Dump-Named $stopWin 40
    Name-Exists 'Stop name' $stopWin | Out-Null
    Name-Exists 'Stop address' $stopWin | Out-Null
    Name-Exists 'Street address' $stopWin | Out-Null
    Click-AutomationName 'Cancel stop' 800 $stopWin | Out-Null
    Start-Sleep -Milliseconds 500
    if ($null -ne (Find-WindowContains 'Route Stop')) { Close-WindowContains 'Route Stop' | Out-Null }
} else {
    Log 'FAIL no Route Stop window after Add Stop'
}

# --- Trip Board new trip (empty combo hide/fill). Timeline is also modal. ---
Focus-Main
Click-AutomationName 'Activity Timeline' 1800 | Out-Null
$tl = Wait-WindowContains 'Activity Timeline' 10
Dump-Windows
if ($null -ne $tl) {
    Dump-Named $tl 40
    Click-AutomationName 'Open trip board' 1600 $tl | Out-Null
    $board = Wait-WindowContains 'Trip Board' 10
    Dump-Windows
    if ($null -ne $board) {
        Dump-Named $board 40
        Click-AutomationName 'New trip' 2000 $board | Out-Null
        $trip = Wait-WindowContains 'New trip' 10
        Dump-Windows
        if ($null -ne $trip) {
            Dump-Named $trip 50
            Name-Exists 'Trip origin' $trip | Out-Null
            Name-Exists 'Driver' $trip | Out-Null
            Name-Exists 'Bus' $trip | Out-Null
            Name-Exists 'Trip purpose' $trip | Out-Null
            Name-Exists 'Destination' $trip | Out-Null
            Click-AutomationName 'Cancel trip' 800 $trip | Out-Null
            Start-Sleep -Milliseconds 500
            if ($null -ne (Find-WindowContains 'New trip')) { Close-WindowContains 'New trip' | Out-Null }
        } else {
            Log 'FAIL no New trip window'
        }
        Close-WindowContains 'Trip Board' | Out-Null
    } else {
        Log 'FAIL no Trip Board window'
    }
    Close-WindowContains 'Activity Timeline' | Out-Null
} else {
    Log 'FAIL no Activity Timeline window'
}

Dump-Windows
Log 'DONE harvest'
exit 0
