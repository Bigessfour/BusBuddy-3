# Guest-side probe: env + createSession + one 2d tile. Never prints the API key.
$ErrorActionPreference = "Continue"
Write-Output "=== env ==="
foreach ($scope in @("Process", "User", "Machine")) {
    $billing = [Environment]::GetEnvironmentVariable("GCP_BILLING_PROJECT", $scope)
    $cloud = [Environment]::GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT", $scope)
    $keyVal = [Environment]::GetEnvironmentVariable("GOOGLE_MAPS_API_KEY", $scope)
    $keyLen = 0
    if ($null -ne $keyVal) { $keyLen = $keyVal.Length }
    Write-Output "scope=$scope billing=[$billing] cloud=[$cloud] mapsKeyLen=$keyLen"
}

Write-Output "=== BusBuddy processes ==="
Get-CimInstance Win32_Process -Filter "Name = 'BusBuddy.WPF.exe'" |
ForEach-Object { Write-Output ("pid={0} cmd={1}" -f $_.ProcessId, $_.CommandLine) }

Write-Output "=== recent logs ==="
Get-ChildItem "C:\dev\BusBuddy-3\BusBuddy.WPF\bin\Debug\logs" -ErrorAction SilentlyContinue |
Sort-Object LastWriteTime -Descending |
Select-Object -First 8 |
ForEach-Object { Write-Output ("{0} len={1} {2}" -f $_.Name, $_.Length, $_.LastWriteTime) }

$key = [Environment]::GetEnvironmentVariable("GOOGLE_MAPS_API_KEY", "User")
if ([string]::IsNullOrWhiteSpace($key)) {
    $key = [Environment]::GetEnvironmentVariable("GOOGLE_MAPS_API_KEY", "Machine")
}
if ([string]::IsNullOrWhiteSpace($key)) {
    Write-Output "NO_KEY"
    exit 2
}

$body = '{"mapType":"roadmap","language":"en-US","region":"US"}'
try {
    $sessResp = Invoke-WebRequest -Method Post `
        -Uri ("https://tile.googleapis.com/v1/createSession?key=" + $key) `
        -ContentType "application/json" -Body $body -UseBasicParsing
    Write-Output ("createSessionHTTP={0}" -f $sessResp.StatusCode)
    $sess = $sessResp.Content | ConvertFrom-Json
    $token = $sess.session
    Write-Output ("tokenLen={0}" -f $token.Length)

    $z = 13; $x = 1689; $y = 3215
    $tileUrl = "https://tile.googleapis.com/v1/2dtiles/$z/$x/$y" + "?session=$token&key=$key"
    $tile = Invoke-WebRequest -Uri $tileUrl -Method GET -UseBasicParsing
    $ct = $tile.Headers["Content-Type"]
    Write-Output ("tileHTTP={0} contentType={1} bytes={2}" -f $tile.StatusCode, $ct, $tile.RawContentLength)
} catch {
    Write-Output ("PROBE_FAIL {0}" -f $_.Exception.Message)
    exit 1
}
