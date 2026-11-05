$ErrorActionPreference = 'Stop'
$unit06Root = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'Initialize-Environment.ps1')
$values = @{}
foreach ($raw in Get-Content (Join-Path $unit06Root '.env')) {
    $line = $raw.Trim()
    if (-not $line -or $line.StartsWith('#')) { continue }
    $parts = $line.Split('=', 2)
    if ($parts.Count -ne 2) { throw 'Use NAME=value in the root .env.' }
    $value = $parts[1].Trim()
    if ($value.Length -ge 2 -and (($value.StartsWith('"') -and $value.EndsWith('"')) -or
        ($value.StartsWith("'") -and $value.EndsWith("'")))) {
        $value = $value.Substring(1, $value.Length - 2)
    }
    $values[$parts[0].Trim()] = $value
}
function Get-AssignedValue([string]$Name) {
    $processValue = [Environment]::GetEnvironmentVariable($Name)
    if (-not [string]::IsNullOrWhiteSpace($processValue)) { return $processValue.Trim() }
    if ($values.ContainsKey($Name) -and $values[$Name]) { return $values[$Name] }
    throw "Set $Name in the shared .env."
}
$tenant = Get-AssignedValue 'TENANT_ID'
$subscription = Get-AssignedValue 'SUBSCRIPTION_ID'
& az login --tenant $tenant
if ($LASTEXITCODE -ne 0) { throw 'Azure login failed.' }
& az account set --subscription $subscription
if ($LASTEXITCODE -ne 0) { throw 'Subscription selection failed.' }
