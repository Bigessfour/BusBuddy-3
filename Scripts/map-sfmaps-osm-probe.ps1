# Lists SfMaps embedded resources and probes whether Tile default style resolves.
# Run on Windows guest: powershell -File Scripts\map-sfmaps-resource-probe.ps1
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase

$dll = "C:\dev\BusBuddy-3\BusBuddy.WPF\bin\Debug\net9.0-windows\Syncfusion.SfMaps.WPF.dll"
if (-not (Test-Path $dll)) {
    $dll = Join-Path $env:USERPROFILE ".nuget\packages\syncfusion.sfmaps.wpf\34.2.3\lib\net9.0-windows7.0\Syncfusion.SfMaps.WPF.dll"
}
Write-Output "dll=$dll"
$asm = [Reflection.Assembly]::LoadFrom((Resolve-Path $dll))
Write-Output ("asm=" + $asm.FullName)
foreach ($n in $asm.GetManifestResourceNames()) { Write-Output ("res=$n") }

# Resolve Tile default style from app resources / theme
$tileType = $asm.GetType("Syncfusion.UI.Xaml.Maps.Tile")
Write-Output ("tileType=" + $tileType)
$style = [System.Windows.Application]::Current
# Standalone: create Application if needed
if ($null -eq [System.Windows.Application]::Current) {
    $app = New-Object System.Windows.Application
    $app.ShutdownMode = [System.Windows.ShutdownMode]::OnExplicitShutdown
}

# Load maps assembly generic dictionary the WPF way
$uri = New-Object System.Uri("/Syncfusion.SfMaps.WPF;component/Themes/Generic.xaml", [System.UriKind]::Relative)
try {
    $rd = [System.Windows.Application]::LoadComponent($uri)
    Write-Output ("genericLoad type=" + $rd.GetType().FullName)
} catch {
    Write-Output ("genericLoad FAIL: " + $_.Exception.Message)
}

# Minimal window: stock OSM ImageryLayer only (Syncfusion map-providers sample)
$window = New-Object System.Windows.Window
$window.Title = "SfMap OSM probe"
$window.Width = 900
$window.Height = 700
$mapType = $asm.GetType("Syncfusion.UI.Xaml.Maps.SfMap")
$layerType = $asm.GetType("Syncfusion.UI.Xaml.Maps.ImageryLayer")
$layerTypeEnum = $asm.GetType("Syncfusion.UI.Xaml.Maps.LayerType")
$map = [Activator]::CreateInstance($mapType)
$layer = [Activator]::CreateInstance($layerType)
$map.ZoomLevel = 3
$osm = [Enum]::Parse($layerTypeEnum, "OSM")
$layer.LayerType = $osm
# Center Seattle-ish from Syncfusion samples: Point(lat, lon)
$layer.Center = New-Object System.Windows.Point 47.6, -122.3
[void]$map.Layers.Add($layer)
$window.Content = $map

$log = "C:\dev\BusBuddy-3\BusBuddy.WPF\bin\Debug\logs\sfmap-osm-probe.txt"
New-Item -ItemType Directory -Force -Path (Split-Path $log) | Out-Null

$timer = New-Object System.Windows.Threading.DispatcherTimer
$timer.Interval = [TimeSpan]::FromSeconds(3)
$timer.Add_Tick({
        $timer.Stop()
        $imageryPanelField = $layerType.GetField("imageryPanel", [Reflection.BindingFlags]"Instance,NonPublic")
        $panel = $imageryPanelField.GetValue($layer)
        $count = 0
        $withSource = 0
        if ($null -ne $panel) {
            $count = $panel.Children.Count
            foreach ($child in $panel.Children) {
                $srcProp = $child.GetType().GetProperty("TileImageSource")
                if ($null -ne $srcProp -and $null -ne $srcProp.GetValue($child)) { $withSource++ }
            }
        }
        $url = $layer.UrlTemplate
        $msg = "OSM_PROBE children=$count withSource=$withSource LayerType=$($layer.LayerType) UrlTemplate=[$url] ActualW=$($map.ActualWidth) ActualH=$($map.ActualHeight)"
        Write-Output $msg
        Set-Content -Path $log -Value $msg
        $window.Close()
    })
$timer.Start()
[void]$window.ShowDialog()
Write-Output ("wrote " + $log)
