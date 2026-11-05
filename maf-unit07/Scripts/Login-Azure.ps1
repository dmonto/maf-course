$ErrorActionPreference = 'Stop'
$unit07Root = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'Initialize-Environment.ps1')
# Parse the dotenv file as literal text, never as executable PowerShell.
foreach ($raw in Get-Content (Join-Path $unit07Root '.env')) {
    $line = $raw.Trim()
    if (-not $line -or $line.StartsWith('#')) { continue }
    $split = $line.IndexOf('=')
    if ($split -le 0) { throw 'Use NAME=value in .env.' }
    $name = $line.Substring(0, $split).Trim()
    if ($name -notmatch '^[A-Za-z_][A-Za-z0-9_]*$') { throw 'Invalid dotenv key.' }
    $value = $line.Substring($split + 1).Trim()
    if ($value.Length -ge 2 -and (($value.StartsWith('"') -and $value.EndsWith('"')) -or
        ($value.StartsWith("'") -and $value.EndsWith("'")))) { $value = $value.Substring(1, $value.Length - 2) }
    if (-not [Environment]::GetEnvironmentVariable($name, 'Process')) {
        [Environment]::SetEnvironmentVariable($name, $value, 'Process')
    }
}
foreach ($name in 'TENANT_ID','SUBSCRIPTION_ID','AZURE_OPENAI_ENDPOINT','AZURE_OPENAI_DEPLOYMENT_NAME') {
    $value = [Environment]::GetEnvironmentVariable($name)
    if (-not $value -or $value.Contains('YOUR-')) { throw "Set $name in the shared .env." }
}
az login --tenant $env:TENANT_ID
if ($LASTEXITCODE -ne 0) { throw 'Azure login failed.' }
az account set --subscription $env:SUBSCRIPTION_ID
if ($LASTEXITCODE -ne 0) { throw 'Subscription selection failed.' }
