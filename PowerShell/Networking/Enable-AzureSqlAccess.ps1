param(
  [Parameter(Mandatory=$true)] [string] $SubscriptionId,
  [Parameter(Mandatory=$true)] [string] $ResourceGroupName,
  [Parameter(Mandatory=$true)] [string] $SqlServerName,
  [string] $RuleName
)

# Load helpers from same folder via dot-sourcing
. "$PSScriptRoot/AzureSqlFirewallHelpers.ps1"

Write-Information "Enabling Azure SQL access for current IP..." -InformationAction Continue

$argsHash = @{
  SubscriptionId    = $SubscriptionId
  ResourceGroupName = $ResourceGroupName
  SqlServerName     = $SqlServerName
  Verbose           = $true
}
if ($PSBoundParameters.ContainsKey('RuleName') -and $RuleName) {
  $argsHash.RuleName = $RuleName
}

$rule = Add-AzureSqlFirewallForCaller @argsHash

if ($rule) {
  Write-Output "✅ Allowed IP $($rule.StartIpAddress) on server '$SqlServerName' with rule '$($rule.FirewallRuleName)'."
  Write-Output "ℹ️ Remember to remove this rule when finished if it's temporary."
}
