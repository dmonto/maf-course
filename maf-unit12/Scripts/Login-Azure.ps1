[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$unitRoot = Split-Path -Parent $PSScriptRoot
$envPath = Join-Path $unitRoot '.env'
if (Test-Path -LiteralPath $envPath) {
    foreach ($line in Get-Content -LiteralPath $envPath) {
        $text = $line.Trim()
        if (-not $text -or $text.StartsWith('#')) { continue }
        $eq = $text.IndexOf('=')
        if ($eq -lt 1) { throw 'Use literal KEY=value assignments in .env.' }
        $name = $text.Substring(0, $eq).Trim()
        if ($name -notmatch '^[A-Za-z_][A-Za-z0-9_]*$') { throw 'Invalid .env variable name.' }
        $value = $text.Substring($eq + 1).Trim()
        if ($value.Length -ge 2 -and (($value.StartsWith('"') -and $value.EndsWith('"')) -or ($value.StartsWith("'") -and $value.EndsWith("'")))) {
            $value = $value.Substring(1, $value.Length - 2)
        }
        if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name, 'Process'))) {
            [Environment]::SetEnvironmentVariable($name, $value, 'Process')
        }
    }
}
if ([string]::IsNullOrWhiteSpace($env:TENANT_ID) -or [string]::IsNullOrWhiteSpace($env:SUBSCRIPTION_ID)) {
    throw 'Set the assigned TENANT_ID and SUBSCRIPTION_ID in the shared .env.'
}
& az login --tenant $env:TENANT_ID
if ($LASTEXITCODE -ne 0) { throw 'Azure CLI login failed.' }
& az account set --subscription $env:SUBSCRIPTION_ID
if ($LASTEXITCODE -ne 0) { throw 'Subscription selection failed.' }
