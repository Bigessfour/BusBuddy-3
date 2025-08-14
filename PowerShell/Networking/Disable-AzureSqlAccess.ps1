param(
  [Parameter(Mandatory=$true)] [string] $SubscriptionId,
  [Parameter(Mandatory=$true)] [string] $ResourceGroupName,
  [Parameter(Mandatory=$true)] [string] $SqlServerName,
  [Parameter(Mandatory=$true)] [string] $RuleName
)

. "$PSScriptRoot/AzureSqlFirewallHelpers.ps1"

Write-Information "Disabling Azure SQL access rule '$RuleName'..." -InformationAction Continue
Remove-AzureSqlFirewallRuleSafe -SubscriptionId $SubscriptionId -ResourceGroupName $ResourceGroupName -SqlServerName $SqlServerName -RuleName $RuleName -Verbose
