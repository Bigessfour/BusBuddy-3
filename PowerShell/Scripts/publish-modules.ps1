<# Publish local modules to PSGallery. Requires $apiKey set or passed as parameter. #>
param(
    [string] $apiKey
)

if (-not $apiKey) {
    Write-Error 'You must supply a NuGet API key for Publish-Module.'
    exit 2
}

$modules = Get-ChildItem -Directory -Path "$(Split-Path -LiteralPath $PSScriptRoot -Parent)\Modules"
foreach ($m in $modules) {
    Write-Information "Publishing module: $($m.Name)" -InformationAction Continue
    Publish-Module -Path $m.FullName -NuGetApiKey $apiKey -Repository PSGallery -Force -ErrorAction Stop
}

nWrite-Output 'Done'
