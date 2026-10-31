#Requires -Version 5.1
# Click header Fleet Maintenance once (schtasks /IT).
$ErrorActionPreference = 'Continue'
$LogPath = "$env:TEMP\busbuddy-uia-maint-$PID.log"
function Log([string]$m) { $l = '{0} {1}' -f (Get-Date -Format 'HH:mm:ss'), $m; Add-Content $LogPath $l; Write-Host $l }

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System; using System.Runtime.InteropServices;
public class MaintClicker {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint x, uint y, uint d, UIntPtr e);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  public static void Click(int x, int y) {
    SetCursorPos(x, y); mouse_event(2, 0, 0, 0, UIntPtr.Zero); mouse_event(4, 0, 0, 0, UIntPtr.Zero);
  }
}
"@

Start-Sleep -Seconds 6
[System.Windows.Forms.SendKeys]::SendWait('{ESC}')
Start-Sleep -Milliseconds 500

$root = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::NameProperty,
    'BusBuddy - School Transportation')
$main = $null
for ($i = 0; $i -lt 30; $i++) {
    $main = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
    if ($null -ne $main) { break }
    Start-Sleep -Seconds 1
}
if ($null -eq $main) { Log 'FAIL no main'; exit 2 }

$hwnd = [IntPtr]$main.Current.NativeWindowHandle
[MaintClicker]::SetForegroundWindow($hwnd) | Out-Null
[System.Windows.Forms.SendKeys]::SendWait('{ESC}')
Start-Sleep -Milliseconds 400

$nameCond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::NameProperty, 'Fleet Maintenance')
$btn = $main.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $nameCond)
if ($null -eq $btn) { Log 'FAIL no Fleet Maintenance button'; exit 3 }
$rect = $btn.Current.BoundingRectangle
$x = [int]($rect.X + $rect.Width / 2)
$y = [int]($rect.Y + $rect.Height / 2)
[MaintClicker]::Click($x, $y)
Log "CLICKED Fleet Maintenance at=$x,$y"
Start-Sleep -Seconds 3
Log 'DONE'
Write-Output "LOG=$LogPath"
