#Requires -Version 5.1
<#
.SYNOPSIS
  Interactive-session UIA for AM Special Needs Bus 5 Assignment Schedule + Print.

.DESCRIPTION
  Run via schtasks /IT (not plain SSH). Opens Route Assignments, selects
  AM Special Needs Bus 5, opens RouteScheduleWindow, dumps published clocks,
  then Print (PdfGrid preview). Does not click Time Route, Re-time, or Print PDF.
#>
param(
    [int]$StartupWaitSeconds = 12,
    [string]$RouteName = 'AM Special Needs Bus 5',
    [string]$LogPath = "$env:TEMP\busbuddy-uia-schedule-$PID.log"
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
public class ScheduleSmokeClicker {
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

function Find-PdfPreviewWindow {
    $named = Find-ByName ([System.Windows.Automation.AutomationElement]::RootElement) 'PDF preview'
    if ($null -ne $named) {
        $win = Get-AncestorWindow $named
        if ($null -ne $win) { return $win }
        if ($named.Current.ControlType -eq [System.Windows.Automation.ControlType]::Window) { return $named }
    }

    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Window)
    $wins = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
    foreach ($w in $wins) {
        $nm = $w.Current.Name
        if ([string]::IsNullOrWhiteSpace($nm)) { continue }
        if ($nm -eq 'Route Schedule') { continue }
        if ($nm -like '*schedule*' -or $nm -like '*PDF*') { return $w }
    }
    return $null
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

function Dump-Windows {
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $kids = $root.FindAll(
        [System.Windows.Automation.TreeScope]::Children,
        [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($w in $kids) {
        Log ("TOP name={0} type={1} class={2} enabled={3}" -f $w.Current.Name, $w.Current.ControlType.ProgrammaticName, $w.Current.ClassName, $w.Current.IsEnabled)
    }
    $mainCond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty,
        'BusBuddy - School Transportation')
    $main = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $mainCond)
    if ($null -eq $main) { return }
    $winType = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Window)
    $nested = $main.FindAll([System.Windows.Automation.TreeScope]::Descendants, $winType)
    foreach ($w in $nested) {
        Log ("NESTED name={0} type={1} class={2} enabled={3}" -f $w.Current.Name, $w.Current.ControlType.ProgrammaticName, $w.Current.ClassName, $w.Current.IsEnabled)
    }
}

function Dump-Named($win, [int]$max = 120) {
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
        [System.Windows.Automation.ControlType]::DataItem,
        [System.Windows.Automation.ControlType]::Custom,
        [System.Windows.Automation.ControlType]::ListItem
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

function Dump-ClockTexts($win) {
    if ($null -eq $win) { return }
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Text)
    $els = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
    $clocks = @()
    foreach ($el in $els) {
        $nm = $el.Current.Name
        if ($nm -match '^\d{2}:\d{2}$' -or $nm -match '^\d{2}:\d{2}.+\d{2}:\d{2}$') {
            $clocks += $nm
            Log ("CLOCK text={0}" -f $nm)
        }
    }
    Log ("CLOCK_COUNT={0}" -f $clocks.Count)
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

function Wait-WindowContains([string]$needle, [int]$timeoutSec = 12) {
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    while ((Get-Date) -lt $deadline) {
        $win = Find-WindowContains $needle
        if ($null -ne $win) {
            Log ("WINDOW hit needle={0} title={1}" -f $needle, $win.Current.Name)
            return $win
        }
        Start-Sleep -Milliseconds 400
    }
    Log "WINDOW timeout needle=$needle"
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
    if ($hwnd -ne [IntPtr]::Zero) { [ScheduleSmokeClicker]::SetForegroundWindow($hwnd) | Out-Null }
    Log 'FOCUS main'
    Start-Sleep -Milliseconds 400
}

function Close-WindowContains([string]$needle) {
    $win = Find-WindowContains $needle
    if ($null -eq $win) {
        Log "CLOSE miss needle=$needle"
        return $false
    }
    try {
        $hwnd = [IntPtr]$win.Current.NativeWindowHandle
        if ($hwnd -ne [IntPtr]::Zero) { [ScheduleSmokeClicker]::SetForegroundWindow($hwnd) | Out-Null }
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

function Get-NamedAll([string]$name, $root = $null) {
    if ($null -eq $root) { $root = [System.Windows.Automation.AutomationElement]::RootElement }
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, $name)
    return $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
}

function Pick-Clickable($els, [string]$name) {
    $best = $null
    $bestScore = [double]::MaxValue
    $n = 0
    foreach ($el in $els) {
        $r = $el.Current.BoundingRectangle
        Log ("CANDIDATE name={0} n={1} offscreen={2} enabled={3} rect={4:N0},{5:N0} {6:N0}x{7:N0}" -f $name, $n, $el.Current.IsOffscreen, $el.Current.IsEnabled, $r.X, $r.Y, $r.Width, $r.Height)
        $n++
        if ($el.Current.IsOffscreen) { continue }
        if (-not $el.Current.IsEnabled) { continue }
        if ($r.Width -lt 24 -or $r.Height -lt 16) { continue }
        # ButtonAdv labels are short; skip giant host rects that miss the control.
        $heightPenalty = 0
        if ($r.Height -gt 80) { $heightPenalty = 100000 + $r.Height }
        $score = ($r.Width * $r.Height) + $heightPenalty
        if ($score -lt $bestScore) {
            $best = $el
            $bestScore = $score
        }
    }
    return $best
}

function Click-Element($el, [string]$label, [int]$pauseMs = 900) {
    if ($null -eq $el) { return $false }
    try {
        $inv = $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
        if ($null -ne $inv) {
            $inv.Invoke()
            Log "INVOKE name=$label"
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
        if ($hwnd -ne [IntPtr]::Zero) { [ScheduleSmokeClicker]::SetForegroundWindow($hwnd) | Out-Null }
        $pt = $el.GetClickablePoint()
        [ScheduleSmokeClicker]::Click([int]$pt.X, [int]$pt.Y)
        Log "CLICK name=$label at=$([int]$pt.X),$([int]$pt.Y)"
        Start-Sleep -Milliseconds $pauseMs
        return $true
    } catch {
        $rect = $el.Current.BoundingRectangle
        if ($rect.Width -gt 0 -and $rect.Height -gt 0) {
            $x = [int]($rect.X + ($rect.Width / 2))
            $y = [int]($rect.Y + ($rect.Height / 2))
            [ScheduleSmokeClicker]::Click($x, $y)
            Log "CLICK-FALLBACK name=$label at=$x,$y"
            Start-Sleep -Milliseconds $pauseMs
            return $true
        }
        Log "FAIL name=$label err=$($_.Exception.Message)"
        return $false
    }
}

function Click-AutomationName([string]$name, [int]$pauseMs = 900, $root = $null) {
    if ($null -eq $root) { $root = [System.Windows.Automation.AutomationElement]::RootElement }
    $els = Get-NamedAll $name $root
    if ($els.Count -eq 0) {
        Log "MISS name=$name"
        return $false
    }
    $el = Pick-Clickable $els $name
    if ($null -eq $el) { $el = $els[0] }
    Log ("HIT name={0} enabled={1} offscreen={2} matches={3}" -f $name, $el.Current.IsEnabled, $el.Current.IsOffscreen, $els.Count)
    return Click-Element $el $name $pauseMs
}

function Wait-EnabledName([string]$name, [int]$timeoutSec = 20) {
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    while ((Get-Date) -lt $deadline) {
        $el = Find-ByName ([System.Windows.Automation.AutomationElement]::RootElement) $name
        if ($null -ne $el -and $el.Current.IsEnabled) {
            Log "ENABLED name=$name"
            return $el
        }
        Start-Sleep -Milliseconds 400
    }
    Log "ENABLED timeout name=$name"
    return $null
}

function Select-ComboItem([string]$comboName, [string]$itemName) {
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $combo = Find-ByName $root $comboName
    if ($null -eq $combo) {
        Log "MISS combo=$comboName"
        return $false
    }

    Log ("COMBO name={0} valueish={1}" -f $comboName, $combo.Current.Name)
    if ($combo.Current.Name -like "*$itemName*") {
        Log "COMBO already shows $itemName"
        return $true
    }

    Click-Element $combo $comboName 500 | Out-Null
    try {
        $exp = $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
        if ($null -ne $exp) {
            $exp.Expand()
            Log "EXPAND $comboName"
            Start-Sleep -Milliseconds 500
        }
    } catch { }

    $itemType = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::ListItem)
    $itemNameCond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, $itemName)
    $and = New-Object System.Windows.Automation.AndCondition($itemType, $itemNameCond)
    $item = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $and)
    if ($null -eq $item) {
        $item = Find-ByName $root $itemName
    }
    if ($null -eq $item) {
        Log "MISS combo item=$itemName"
        $items = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $itemType)
        $shown = 0
        foreach ($li in $items) {
            if ([string]::IsNullOrWhiteSpace($li.Current.Name)) { continue }
            Log ("  LISTITEM name={0}" -f $li.Current.Name)
            $shown++
            if ($shown -ge 30) { break }
        }
        return $false
    }

    $ok = Click-Element $item $itemName 800
    Start-Sleep -Milliseconds 1200
    return $ok
}

Log "START pid=$PID session=$([System.Diagnostics.Process]::GetCurrentProcess().SessionId) route=$RouteName"
Start-Sleep -Seconds $StartupWaitSeconds

$win = Wait-MainWindow
if ($null -eq $win) { Dump-Windows; exit 2 }
Dump-Windows
Focus-Main

foreach ($needle in @('Dashboard', 'Route Management', 'Settings', 'Reports', 'Route Details', 'PDF')) {
    while ($null -ne (Find-WindowContains $needle)) {
        Log "CLOSE leftover $needle"
        Close-WindowContains $needle | Out-Null
        Start-Sleep -Milliseconds 400
    }
}

Focus-Main
Dump-Windows

Click-AutomationName 'Route Assignments' 1600 | Out-Null
Start-Sleep -Seconds 6
Focus-Main
Dump-Windows
Dump-Windows

if (-not (Select-ComboItem 'Route Selector' $RouteName)) {
    Log "WARN route select missed; continuing if default is $RouteName"
}

$scheduleBtn = Wait-EnabledName 'View Schedule' 25
if ($null -eq $scheduleBtn) {
    Dump-Named $win 80
    Log 'FAIL View Schedule never enabled'
    exit 3
}

# Re-pick the compact ButtonAdv rect; Wait-EnabledName returns FindFirst which can be a host.
$scheduleEls = Get-NamedAll 'View Schedule'
$picked = Pick-Clickable $scheduleEls 'View Schedule'
if ($null -ne $picked) { $scheduleBtn = $picked }

Start-Sleep -Seconds 2
if (-not (Click-Element $scheduleBtn 'View Schedule' 2500)) {
    Log 'FAIL View Schedule click'
    exit 3
}

$sched = $null
$deadline = (Get-Date).AddSeconds(12)
while ((Get-Date) -lt $deadline) {
    $named = Find-ByName ([System.Windows.Automation.AutomationElement]::RootElement) 'Route Schedule'
    if ($null -ne $named) {
        $sched = Get-AncestorWindow $named
        if ($null -eq $sched) { $sched = $named }
        Log ("WINDOW hit Route Schedule title={0} type={1}" -f $sched.Current.Name, $sched.Current.ControlType.ProgrammaticName)
        break
    }
    $sched = Find-WindowContains 'Route Schedule'
    if ($null -ne $sched) { break }
    Start-Sleep -Milliseconds 400
}
Dump-Windows
if ($null -eq $sched) {
    Log 'FAIL no Route Schedule window'
    exit 4
}

Dump-Named $sched 150
Dump-ClockTexts $sched

# Print on the schedule window only — never Print PDF on the preview.
if (-not (Click-AutomationName 'Print route schedule' 2500 $sched)) {
    Log 'FAIL Print route schedule miss'
    Dump-Named $sched 40
    exit 5
}

$preview = $null
$deadline = (Get-Date).AddSeconds(15)
while ((Get-Date) -lt $deadline) {
    $preview = Find-PdfPreviewWindow
    if ($null -ne $preview) {
        Log ("WINDOW hit PDF preview title={0}" -f $preview.Current.Name)
        break
    }
    Start-Sleep -Milliseconds 400
}
Dump-Windows
if ($null -eq $preview) {
    Log 'FAIL no PDF preview window'
    exit 6
}

Log ("PREVIEW title={0}" -f $preview.Current.Name)
Dump-Named $preview 40
$printPdf = Find-ByName $preview 'Print PDF'
if ($null -ne $printPdf) {
    Log 'PRESENT Print PDF (not clicked — preview only)'
}

if (-not (Click-AutomationName 'Close PDF preview' 800 $preview)) {
    try {
        $wp = $preview.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern)
        $wp.Close()
        Log 'CLOSE PDF preview via WindowPattern'
        Start-Sleep -Milliseconds 700
    } catch {
        Log "CLOSE PDF preview fail err=$($_.Exception.Message)"
    }
}
Start-Sleep -Milliseconds 400
if ($null -ne (Find-WindowContains 'Route Schedule')) {
    Click-AutomationName 'Close route schedule' 800 | Out-Null
    if ($null -ne (Find-WindowContains 'Route Schedule')) { Close-WindowContains 'Route Schedule' | Out-Null }
}

Dump-Windows
Log 'DONE schedule smoke'
exit 0
